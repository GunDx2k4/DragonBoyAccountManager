using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Automation.Behaviors.Lifecycle;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine;

public sealed class SessionWorker : IDisposable
{
    public GameSession Session { get; }
    public AccountLaunchOptions Options { get; private set; }
    public CancellationTokenSource Cts { get; } = new();
    public Task? LoopTask { get; private set; }

    public bool HasAssignedSkills { get; set; }
    public float GameSpeed { get; private set; } = 1.0f;
    public bool IsMasked { get; private set; }
    public string? OriginalCharacterName { get; private set; }
    public string? OriginalServerListCaption { get; private set; }

    private int _isLoggingIn;
    public bool IsLoggingIn => Volatile.Read(ref _isLoggingIn) == 1;

    private int _isAutoTrainRunning;
    public bool IsAutoTrainRunning
    {
        get => Volatile.Read(ref _isAutoTrainRunning) == 1;
        set => Interlocked.Exchange(ref _isAutoTrainRunning, value ? 1 : 0);
    }

    private int _isAutoChatRunning;
    public bool IsAutoChatRunning
    {
        get => Volatile.Read(ref _isAutoChatRunning) == 1;
        set => Interlocked.Exchange(ref _isAutoChatRunning, value ? 1 : 0);
    }

    public event Action<SessionEvent>? EventPublished;

    public event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? SessionUpdated;
    public event Action<Guid, string>? MessageUpdated;
    public event Action<Guid, ActionExecutionReport>? ActionReported;
    public event Action<Guid, string, int>? ChatSent;
    public event Action<Guid, string>? AutoTrainStatusChanged;
    public event Action<Guid, bool>? AutoTrainStateChanged;

    public void Publish(SessionEvent evt)
    {
        EventPublished?.Invoke(evt);

        switch (evt)
        {
            case SessionStateChangedEvent e:
                SessionUpdated?.Invoke(e.AccountId, e.Status, e.Snapshot);
                break;
            case SessionMessageEvent e:
                MessageUpdated?.Invoke(e.AccountId, e.Message);
                break;
            case ActionReportedEvent e:
                ActionReported?.Invoke(e.AccountId, e.Report);
                break;
            case ChatSentEvent e:
                ChatSent?.Invoke(e.AccountId, e.Message, e.Channel);
                break;
            case BehaviorStateChangedEvent e when e.BehaviorId == "AutoTrain":
                AutoTrainStateChanged?.Invoke(e.AccountId, e.IsRunning);
                if (e.StatusMessage != null)
                    AutoTrainStatusChanged?.Invoke(e.AccountId, e.StatusMessage);
                break;
            case BehaviorProgressEvent e when e.BehaviorId == "AutoTrain":
                AutoTrainStatusChanged?.Invoke(e.AccountId, e.Message);
                break;
        }
    }

    public SessionWorker(GameSession session, AccountLaunchOptions options)
    {
        Session = session;
        Options = options;
        GameSpeed = options.GameSpeed;
        IsMasked = options.IsMasked;
    }

    public void StartLoop()
    {
        LoopTask = Task.Run(RunLoopAsync);
    }

    private async Task RunLoopAsync()
    {
        var token = Cts.Token;
        while (!token.IsCancellationRequested && Session.IsRunning)
        {
            try
            {
                if (Session.IsQuitRequested)
                {
                    await StopAsync();
                    break;
                }

                Session.Tick();

                if (IsMasked || !string.IsNullOrEmpty(OriginalServerListCaption))
                {
                    ApplyEarlyMaskingGuard();
                }

                if (Session.CurrentPlayer.IsInGame)
                {
                    SyncInGameCharacterNameMasking();

                    if (!HasAssignedSkills && Options.AutoAssignSkills)
                    {
                        HasAssignedSkills = true;
                        _ = Task.Run(() => AutoAssignSkillsAsync(token), token);
                    }

                    PublishSessionUpdate();
                }
                else
                {
                    HasAssignedSkills = false;
                    OriginalCharacterName = null;
                    if (Options.AutoLogin && !IsLoggingIn && Session.IsRunning && !Session.IsQuitRequested && !Cts.IsCancellationRequested)
                    {
                        _ = Task.Run(async () =>
                        {
                            try { await ExecuteLoginAsync(); } catch { }
                        }, token);
                    }
                }

                int delayMs = Session.CurrentPlayer.IsInGame ? 500 : 200;
                await Task.Delay(delayMs, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(1000, token);
            }
        }
    }

    public void PublishSessionUpdate()
    {
        if (!Session.IsRunning || Cts.IsCancellationRequested) return;

        try
        {
            if (Session.CurrentPlayer.IsInGame)
            {
                var snapshot = new GameSessionSnapshot(Session.CurrentPlayer, Session.CurrentWorld);
                Publish(new SessionStateChangedEvent(Options.AccountId, GameSessionStatus.Active, snapshot));
            }
        }
        catch
        {
        }
    }

    public async Task<bool> ExecuteLoginAsync()
    {
        if (Interlocked.CompareExchange(ref _isLoggingIn, 1, 0) != 0)
            return false;

        if (Session.IsQuitRequested || !Session.IsRunning)
        {
            Interlocked.Exchange(ref _isLoggingIn, 0);
            return false;
        }

        Publish(new SessionStateChangedEvent(Options.AccountId, GameSessionStatus.LoggingIn, null));
        Publish(new SessionMessageEvent(Options.AccountId, "Bắt đầu đăng nhập..."));

        var progress = new Progress<string>(msg =>
        {
            Publish(new SessionMessageEvent(Options.AccountId, msg));
        });

        try
        {
            var loginOptions = new AutoLoginOptions
            {
                Username = Options.Username,
                Password = Options.Password,
                ServerId = Options.ServerId,
                ServerName = Options.ServerName,
                MaxAttempts = Options.AutoLogin ? 15 : 3,
                IsMasked = IsMasked
            };

            var behavior = new AutoLoginBehavior(loginOptions, Session.Mono);
            await behavior.StartAsync(
                () => Session.PlayerScraper.ScrapePlayer(),
                () => Session.WorldScraper.ScrapeWorld(),
                () => Session.MapScraper.ScrapeMap(),
                Session.Dispatcher,
                progress,
                Cts.Token);

            Session.Tick();
            if (Session.CurrentPlayer.IsInGame)
            {
                Publish(new SessionMessageEvent(Options.AccountId, "Đăng nhập thành công!"));
                if (!HasAssignedSkills && Options.AutoAssignSkills)
                {
                    HasAssignedSkills = true;
                    _ = Task.Run(() => AutoAssignSkillsAsync());
                }

                var snapshot = new GameSessionSnapshot(Session.CurrentPlayer, Session.CurrentWorld);
                Publish(new SessionStateChangedEvent(Options.AccountId, GameSessionStatus.Active, snapshot));
                return true;
            }

            if (Session.IsRunning && !Cts.IsCancellationRequested)
            {
                Publish(new SessionStateChangedEvent(Options.AccountId, GameSessionStatus.Running, null));
                Publish(new SessionMessageEvent(Options.AccountId, "Đăng nhập chưa hoàn tất."));
            }
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            if (Session.IsRunning && !Cts.IsCancellationRequested)
            {
                Publish(new SessionStateChangedEvent(Options.AccountId, GameSessionStatus.Running, null));
                Publish(new SessionMessageEvent(Options.AccountId, $"Lỗi đăng nhập: {ex.Message}"));
            }
            return false;
        }
        finally
        {
            Interlocked.Exchange(ref _isLoggingIn, 0);
        }
    }

    public async Task<Result> AutoAssignSkillsAsync(CancellationToken cancellationToken = default)
    {
        if (Session.IsRunning && Session.CurrentPlayer.IsInGame)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token, cancellationToken);
            var res = await Session.Dispatcher.DispatchAsync(new AssignSkillsCommand(), linkedCts.Token);
            var report = res.IsSuccess
                ? ActionExecutionReport.Success("Gán phím kỹ năng", res.Message)
                : ActionExecutionReport.Failure("Gán phím kỹ năng", res.Message);
            Publish(new ActionReportedEvent(Options.AccountId, report));
            return res;
        }
        return Result.Failure("Tài khoản chưa vào game.");
    }

    public void StartAutoTrain(AutoTrainOptions options)
    {
        if (!Session.IsRunning) return;

        StopAutoTrain();

        Session.TrainBehavior.UpdateOptions(options);
        IsAutoTrainRunning = true;

        var progress = new Progress<string>(msg =>
        {
            Publish(new BehaviorProgressEvent(Options.AccountId, "AutoTrain", msg));
            Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Running("Tự động luyện tập", msg)));
        });

        Publish(new BehaviorStateChangedEvent(Options.AccountId, "AutoTrain", true));

        _ = Session.TrainBehavior.StartAsync(
            () => Session.PlayerScraper.ScrapePlayer(),
            () => Session.WorldScraper.ScrapeWorld(),
            () => Session.MapScraper.ScrapeMap(),
            Session.Dispatcher,
            progress,
            Cts.Token);
    }

    public void StopAutoTrain()
    {
        Session.TrainBehavior.Stop();
        IsAutoTrainRunning = false;
        Publish(new BehaviorStateChangedEvent(Options.AccountId, "AutoTrain", false, "Đã dừng tự động luyện tập."));
        Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Success("Tự động luyện tập", "Đã dừng tự động luyện tập.")));
    }

    public void StartAutoChat(AutoChatOptions options)
    {
        if (!Session.IsRunning) return;

        StopAutoChat();
        Session.ChatBehavior.UpdateOptions(options);
        IsAutoChatRunning = true;

        var progress = new Progress<string>(msg =>
        {
            Publish(new ChatSentEvent(Options.AccountId, msg, 1));
        });

        _ = Session.ChatBehavior.StartAsync(
            () => Session.PlayerScraper.ScrapePlayer(),
            () => Session.WorldScraper.ScrapeWorld(),
            () => Session.MapScraper.ScrapeMap(),
            Session.Dispatcher,
            progress,
            Cts.Token);
    }

    public void StopAutoChat()
    {
        Session.ChatBehavior.Stop();
        IsAutoChatRunning = false;
    }

    public bool StartBehavior(string behaviorId)
    {
        var behavior = Session.GetBehavior(behaviorId);
        if (behavior == null || !Session.IsRunning) return false;

        var progress = new Progress<string>(msg =>
        {
            Publish(new BehaviorProgressEvent(Options.AccountId, behavior.Id, msg));
            Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Running(behavior.Name, msg)));
        });

        Publish(new BehaviorStateChangedEvent(Options.AccountId, behavior.Id, true));
        _ = behavior.StartAsync(Session, progress, Cts.Token);
        return true;
    }

    public bool StopBehavior(string behaviorId)
    {
        var behavior = Session.GetBehavior(behaviorId);
        if (behavior == null) return false;

        behavior.Stop();
        Publish(new BehaviorStateChangedEvent(Options.AccountId, behavior.Id, false, $"Đã dừng {behavior.Name}."));
        Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Success(behavior.Name, $"Đã dừng {behavior.Name}.")));
        return true;
    }

    public bool IsBehaviorRunning(string behaviorId)
    {
        var behavior = Session.GetBehavior(behaviorId);
        return behavior?.IsRunning ?? false;
    }

    public IReadOnlyCollection<IBehavior> GetBehaviors() => Session.Behaviors.ToList();

    public void SetGameSpeed(float speed)
    {
        GameSpeed = speed;
        try
        {
            Session.Mono.CallUnityMethod("UnityEngine", "Time", "set_timeScale", IntPtr.Zero, speed);
        }
        catch
        {
        }
    }

    public void UpdateLaunchOptions(AccountLaunchOptions options)
    {
        Options = options;
    }

    public void SetMasked(bool isMasked)
    {
        IsMasked = isMasked;
        Options = Options with { IsMasked = isMasked };
        ApplyEarlyMaskingGuard();
        SyncInGameCharacterNameMasking();
    }

    private void ApplyEarlyMaskingGuard()
    {
        if (!Session.IsRunning) return;

        try
        {
            if (IsMasked)
            {
                string curRmsAcc = Session.Mono.ReadStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc);
                if (curRmsAcc != "DBAM")
                {
                    Session.Mono.WriteStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc, "DBAM");
                }

                IntPtr serverScreen = Session.Mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
                if (serverScreen != IntPtr.Zero)
                {
                    IntPtr cmdArr = Session.Mono.ReadFieldPtr(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.FieldCmd, serverScreen);
                    if (cmdArr != IntPtr.Zero)
                    {
                        IntPtr[] cmds = Session.Mono.ReadMonoPointerArray(cmdArr);
                        if (cmds.Length > 0 && cmds[0] != IntPtr.Zero)
                        {
                            string caption = Session.Mono.ReadStringField(GameSchema.Command.ClassName, GameSchema.Command.FieldCaption, cmds[0]) ?? string.Empty;
                            if (caption != "DragonBoy Account Manager")
                            {
                                OriginalServerListCaption ??= caption;
                                Session.Mono.WriteStringField(GameSchema.Command.ClassName, GameSchema.Command.FieldCaption, cmds[0], "DragonBoy Account Manager");
                            }
                        }
                    }
                }

                IntPtr loginScr = Session.Mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
                if (loginScr != IntPtr.Zero)
                {
                    IntPtr tfUser = Session.Mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
                    IntPtr tfPass = Session.Mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfPass, loginScr);

                    if (tfUser != IntPtr.Zero)
                    {
                        int inputType = Session.Mono.ReadField<int>(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser);
                        if (inputType != GameSchema.TField.InputTypePassword)
                        {
                            Session.Mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, GameSchema.TField.InputTypePassword);
                        }

                        if (!Session.CurrentPlayer.IsInGame)
                        {
                            string curText = Session.Mono.ReadStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser) ?? string.Empty;
                            if (!string.IsNullOrEmpty(curText) && !string.Equals(curText, Options.Username, StringComparison.Ordinal) && !curText.Contains('*'))
                            {
                                string masked = TextMaskHelper.MaskText(curText);
                                Session.Mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser, masked);
                                Session.Mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText, tfUser, masked);
                            }
                        }
                    }

                    if (tfPass != IntPtr.Zero)
                    {
                        int passInputType = Session.Mono.ReadField<int>(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfPass);
                        if (passInputType != GameSchema.TField.InputTypePassword)
                        {
                            Session.Mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfPass, GameSchema.TField.InputTypePassword);
                        }
                    }
                }
            }
            else
            {
                string curRmsAcc = Session.Mono.ReadStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc);
                if (curRmsAcc != "acc")
                {
                    Session.Mono.WriteStaticString(GameSchema.Rms.ClassName, GameSchema.Rms.StaticRMS_acc, "acc");
                }

                if (!string.IsNullOrEmpty(OriginalServerListCaption))
                {
                    IntPtr serverScreen = Session.Mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticServerScreen);
                    if (serverScreen != IntPtr.Zero)
                    {
                        IntPtr cmdArr = Session.Mono.ReadFieldPtr(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.FieldCmd, serverScreen);
                        if (cmdArr != IntPtr.Zero)
                        {
                            IntPtr[] cmds = Session.Mono.ReadMonoPointerArray(cmdArr);
                            if (cmds.Length > 0 && cmds[0] != IntPtr.Zero)
                            {
                                Session.Mono.WriteStringField(GameSchema.Command.ClassName, GameSchema.Command.FieldCaption, cmds[0], OriginalServerListCaption);
                            }
                        }
                    }
                    OriginalServerListCaption = null;
                }

                IntPtr loginScr = Session.Mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
                if (loginScr != IntPtr.Zero)
                {
                    IntPtr tfUser = Session.Mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
                    if (tfUser != IntPtr.Zero)
                    {
                        int inputType = Session.Mono.ReadField<int>(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser);
                        if (inputType == GameSchema.TField.InputTypePassword)
                        {
                            Session.Mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, 0);
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void SyncInGameCharacterNameMasking()
    {
        if (!Session.IsRunning || !Session.CurrentPlayer.IsInGame) return;

        try
        {
            IntPtr myCharPtr = Session.Mono.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
            if (myCharPtr != IntPtr.Zero)
            {
                string currentCName = Session.Mono.ReadStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, myCharPtr) ?? string.Empty;

                if (IsMasked)
                {
                    if (!string.IsNullOrWhiteSpace(currentCName) && !currentCName.Contains('*'))
                    {
                        OriginalCharacterName ??= currentCName;
                        string masked = TextMaskHelper.MaskText(currentCName);
                        Session.Mono.WriteStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, myCharPtr, masked);
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(OriginalCharacterName) && currentCName != OriginalCharacterName)
                    {
                        Session.Mono.WriteStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, myCharPtr, OriginalCharacterName);
                        OriginalCharacterName = null;
                    }
                }
            }

            IntPtr loginScr = Session.Mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticLoginScr);
            if (loginScr != IntPtr.Zero)
            {
                IntPtr tfUser = Session.Mono.ReadFieldPtr(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.FieldTfUser, loginScr);
                if (tfUser != IntPtr.Zero)
                {
                    string currentTfUser = Session.Mono.ReadStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser) ?? string.Empty;
                    if (IsMasked)
                    {
                        string maskedUser = TextMaskHelper.MaskText(Options.Username);
                        if (!string.IsNullOrEmpty(maskedUser) && currentTfUser != maskedUser)
                        {
                            Session.Mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser, maskedUser);
                            Session.Mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText, tfUser, maskedUser);
                            Session.Mono.WriteField(GameSchema.TField.ClassName, GameSchema.TField.FieldInputType, tfUser, GameSchema.TField.InputTypePassword);
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(Options.Username) && currentTfUser != Options.Username)
                        {
                            Session.Mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldText, tfUser, Options.Username);
                            Session.Mono.WriteStringField(GameSchema.TField.ClassName, GameSchema.TField.FieldPaintedText, tfUser, Options.Username);
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    public async Task StopAsync()
    {
        try { Cts.Cancel(); } catch { }
        Session.StopAllBehaviors();
        Session.Kill();
        if (LoopTask != null)
        {
            try { await LoopTask; } catch { }
        }
        Cts.Dispose();
        Session.Dispose();
    }

    public void Dispose()
    {
        try { Cts.Cancel(); } catch { }
        Session.StopAllBehaviors();
        Session.Kill();
        Cts.Dispose();
        Session.Dispose();
        GC.SuppressFinalize(this);
    }
}
