using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Automation.Behaviors.Lifecycle;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Execution;
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

    private int _isLoggingIn;
    public bool IsLoggingIn => Volatile.Read(ref _isLoggingIn) == 1;
    private readonly ConcurrentDictionary<string, byte> _activeBehaviors = new(StringComparer.OrdinalIgnoreCase);

    public event Action<SessionEvent>? EventPublished;

    public void Publish(SessionEvent evt)
    {
        EventPublished?.Invoke(evt);
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

                if (IsMasked || Session.MaskingController.HasOriginalServerListCaption)
                {
                    Session.MaskingController.ApplyEarlyMaskingGuard(IsMasked, Session.CurrentPlayer.IsInGame, Options.Username);
                }

                if (Session.CurrentPlayer.IsInGame)
                {
                    Session.MaskingController.SyncInGameCharacterNameMasking(IsMasked, true, Options.Username);

                    if (!HasAssignedSkills && Options.AutoAssignSkills)
                    {
                        HasAssignedSkills = true;
                        _ = Task.Run(() => ExecuteCommandAsync(new AssignSkillsCommand(), token), token);
                    }

                    PublishSessionUpdate();
                }
                else
                {
                    HasAssignedSkills = false;
                    Session.MaskingController.ResetCharacterName();
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
                var snapshot = Session.CreateSessionSnapshot();
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

            var behavior = new AutoLoginBehavior(loginOptions, Session.LoginScreen);
            await behavior.StartAsync(Session, progress, Cts.Token);

            Session.Tick();
            if (Session.CurrentPlayer.IsInGame)
            {
                Publish(new SessionMessageEvent(Options.AccountId, "Đăng nhập thành công!"));
                if (!HasAssignedSkills && Options.AutoAssignSkills)
                {
                    HasAssignedSkills = true;
                    _ = Task.Run(() => ExecuteCommandAsync(new AssignSkillsCommand()));
                }

                var snapshot = Session.CreateSessionSnapshot();
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

    public async Task<Result> ExecuteCommandAsync(IGameCommand command, CancellationToken cancellationToken = default)
    {
        if (Session.IsRunning)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(Cts.Token, cancellationToken);
            var res = await Session.Dispatcher.DispatchAsync(command, linkedCts.Token);
            var report = res.IsSuccess
                ? ActionExecutionReport.Success(command.Name, res.Message)
                : ActionExecutionReport.Failure(command.Name, res.Message);
            Publish(new ActionReportedEvent(Options.AccountId, report));
            return res;
        }
        return Result.Failure("Tài khoản chưa vào game.");
    }

    public bool StartBehavior<TBehavior, TOptions>(TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class
    {
        if (!Session.IsRunning) return false;

        var behavior = Session.GetBehavior<TBehavior>();
        if (behavior == null) return false;

        behavior.UpdateOptions(options);
        if (behavior.IsRunning)
        {
            return true;
        }

        return LaunchBehaviorInternal(behavior);
    }

    public bool UpdateBehaviorOptions<TBehavior, TOptions>(TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class
    {
        if (!Session.IsRunning) return false;

        var behavior = Session.GetBehavior<TBehavior>();
        if (behavior == null) return false;

        behavior.UpdateOptions(options);
        return true;
    }

    public bool StartBehavior(string behaviorId)
    {
        var behavior = Session.GetBehavior(behaviorId);
        if (behavior == null || !Session.IsRunning) return false;

        return LaunchBehaviorInternal(behavior);
    }

    private bool LaunchBehaviorInternal(IBehavior behavior)
    {
        if (behavior.IsRunning) return true;

        var progress = new Progress<string>(msg =>
        {
            Publish(new BehaviorProgressEvent(Options.AccountId, behavior.Id, msg));
            Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Running(behavior.Name, msg)));
        });

        _activeBehaviors[behavior.Id] = 1;
        Publish(new BehaviorStateChangedEvent(Options.AccountId, behavior.Id, true));
        _ = RunBehaviorLifecycleAsync(behavior, progress);
        return true;
    }

    private async Task RunBehaviorLifecycleAsync(IBehavior behavior, IProgress<string> progress)
    {
        try
        {
            await behavior.StartAsync(Session, progress, Cts.Token);
        }
        finally
        {
            if (_activeBehaviors.TryRemove(behavior.Id, out _) && !Cts.IsCancellationRequested)
            {
                Publish(new BehaviorStateChangedEvent(Options.AccountId, behavior.Id, false, $"Đã dừng {behavior.Name}."));
                Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Success(behavior.Name, $"Đã dừng {behavior.Name}.")));
            }
        }
    }

    public bool StopBehavior<TBehavior>() where TBehavior : class, IBehavior
    {
        var behavior = Session.GetBehavior<TBehavior>();
        return behavior != null && StopBehaviorInternal(behavior);
    }

    public bool StopBehavior(string behaviorId)
    {
        var behavior = Session.GetBehavior(behaviorId);
        return behavior != null && StopBehaviorInternal(behavior);
    }

    private bool StopBehaviorInternal(IBehavior behavior)
    {
        behavior.Stop();
        if (_activeBehaviors.TryRemove(behavior.Id, out _))
        {
            Publish(new BehaviorStateChangedEvent(Options.AccountId, behavior.Id, false, $"Đã dừng {behavior.Name}."));
            Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Success(behavior.Name, $"Đã dừng {behavior.Name}.")));
        }
        return true;
    }

    public bool IsBehaviorRunning<TBehavior>() where TBehavior : class, IBehavior
    {
        var behavior = Session.GetBehavior<TBehavior>();
        return behavior?.IsRunning ?? false;
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
        if (Session.IsRunning)
        {
            Session.MaskingController.ApplyEarlyMaskingGuard(IsMasked, Session.CurrentPlayer.IsInGame, Options.Username);
            Session.MaskingController.SyncInGameCharacterNameMasking(IsMasked, Session.CurrentPlayer.IsInGame, Options.Username);
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
