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

namespace DragonBoyAccountManager.Core.Engine;

public sealed class SessionWorker : IDisposable, IAsyncDisposable
{
    public GameSession Session { get; }
    public AccountLaunchOptions Options { get; private set; }
    public CancellationTokenSource Cts { get; } = new();
    public Task? LoopTask { get; private set; }

    public bool HasAssignedSkills { get; set; }
    public bool IsMasked => Session.MaskingController.IsMasked;

    private int _isLoggingIn;
    private volatile bool _wasInGame;
    private long _behaviorRunSequence;
    public bool IsLoggingIn => Volatile.Read(ref _isLoggingIn) == 1;
    private readonly ConcurrentDictionary<string, long> _activeBehaviors = new(StringComparer.OrdinalIgnoreCase);

    public event Action<SessionEvent>? EventPublished;

    public void Publish(SessionEvent evt)
    {
        EventPublished?.Invoke(evt);
    }

    public SessionWorker(GameSession session, AccountLaunchOptions options)
    {
        Session = session;
        Options = options;
        bool isInGame = Session.CurrentPlayer.IsInGame;
        Session.MaskingController.Configure(options.IsMasked, options.Username, isInGame);
    }

    public void StartLoop()
    {
        LoopTask = Task.Run(RunLoopAsync);
    }

    private void TryAutoAssignSkills(CancellationToken token = default)
    {
        if (!HasAssignedSkills && Options.AutoAssignSkills)
        {
            HasAssignedSkills = true;
            _ = Task.Run(() => ExecuteCommandAsync(new AssignSkillsCommand(), token), token);
        }
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

                if (Session.CurrentPlayer.IsInGame)
                {
                    TryAutoAssignSkills(token);
                    PublishSessionUpdate();
                }
                else
                {
                    HasAssignedSkills = false;
                    if (_wasInGame && Options.AutoLogin && !IsLoggingIn && Session.IsRunning && !Session.IsQuitRequested && !Cts.IsCancellationRequested)
                    {
                        _ = Task.Run(async () =>
                        {
                            _wasInGame = false;
                            try
                            {
                                bool loggedIn = await ExecuteLoginAsync();
                                if (!loggedIn && Session.IsRunning && !Session.IsQuitRequested && !Cts.IsCancellationRequested)
                                {
                                    await Task.Delay(3000, token);
                                    _wasInGame = true;
                                }
                            }
                            catch { }
                        }, token);
                    }
                }

                int delayMs = Session.CurrentPlayer.IsInGame ? 500 : 200;
                await Task.Delay(delayMs, token);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
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
                _wasInGame = true;
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

        ResumeHighestPriorityExclusiveBehavior();

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
                _wasInGame = true;
                Publish(new SessionMessageEvent(Options.AccountId, "Đăng nhập thành công!"));
                TryAutoAssignSkills(Cts.Token);

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
            ResumeHighestPriorityExclusiveBehavior();
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
        if (_activeBehaviors.ContainsKey(behavior.Id) && behavior.IsRunning)
        {
            ArbitrateExclusiveBehaviors(behavior);
            return true;
        }

        ArbitrateExclusiveBehaviors(behavior);

        var progress = new Progress<string>(msg =>
        {
            Publish(new BehaviorProgressEvent(Options.AccountId, behavior.Id, msg));
            Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Running(behavior.Name, msg)));
        });

        long runId = Interlocked.Increment(ref _behaviorRunSequence);
        _activeBehaviors[behavior.Id] = runId;
        Publish(new BehaviorStateChangedEvent(Options.AccountId, behavior.Id, true));
        _ = RunBehaviorLifecycleAsync(behavior, progress, runId);
        return true;
    }

    private void ArbitrateExclusiveBehaviors(IBehavior incoming)
    {
        if (incoming.ExecutionMode != BehaviorExecutionMode.Exclusive)
            return;

        bool hasHigherPriority = false;
        foreach (var other in Session.Behaviors)
        {
            if (!other.IsRunning || !_activeBehaviors.ContainsKey(other.Id) || string.Equals(other.Id, incoming.Id, StringComparison.OrdinalIgnoreCase) || other.ExecutionMode != BehaviorExecutionMode.Exclusive)
                continue;

            if (other.Priority == incoming.Priority)
            {
                StopBehaviorInternal(other);
            }
            else if (other.Priority < incoming.Priority)
            {
                other.Pause();
            }
            else
            {
                hasHigherPriority = true;
            }
        }

        if (IsLoggingIn || hasHigherPriority)
        {
            incoming.Pause();
        }
        else
        {
            incoming.Resume();
        }
    }

    private void ResumeHighestPriorityExclusiveBehavior()
    {
        if (IsLoggingIn)
        {
            foreach (var b in Session.Behaviors)
            {
                if (b.ExecutionMode == BehaviorExecutionMode.Exclusive)
                {
                    b.Pause();
                }
            }
            return;
        }

        var runningExclusives = Session.Behaviors
            .Where(b => b.IsRunning && _activeBehaviors.ContainsKey(b.Id) && b.ExecutionMode == BehaviorExecutionMode.Exclusive)
            .OrderByDescending(b => b.Priority)
            .ToList();

        for (int i = 0; i < runningExclusives.Count; i++)
        {
            if (i == 0)
                runningExclusives[i].Resume();
            else
                runningExclusives[i].Pause();
        }
    }

    private async Task RunBehaviorLifecycleAsync(IBehavior behavior, IProgress<string> progress, long runId)
    {
        try
        {
            await behavior.StartAsync(Session, progress, Cts.Token);
        }
        finally
        {
            if (_activeBehaviors.TryRemove(KeyValuePair.Create(behavior.Id, runId)) && !Cts.IsCancellationRequested)
            {
                Publish(new BehaviorStateChangedEvent(Options.AccountId, behavior.Id, false, $"Đã dừng {behavior.Name}."));
                Publish(new ActionReportedEvent(Options.AccountId, ActionExecutionReport.Success(behavior.Name, $"Đã dừng {behavior.Name}.")));
            }
            ResumeHighestPriorityExclusiveBehavior();
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
        ResumeHighestPriorityExclusiveBehavior();
        return true;
    }

    public bool IsBehaviorRunning<TBehavior>() where TBehavior : class, IBehavior
    {
        var behavior = Session.GetBehavior<TBehavior>();
        return behavior != null && behavior.IsRunning && _activeBehaviors.ContainsKey(behavior.Id);
    }

    public bool IsBehaviorRunning(string behaviorId)
    {
        var behavior = Session.GetBehavior(behaviorId);
        return behavior != null && behavior.IsRunning && _activeBehaviors.ContainsKey(behavior.Id);
    }

    public IReadOnlyCollection<IBehavior> GetBehaviors() => Session.Behaviors.ToList();

    public void UpdateLaunchOptions(AccountLaunchOptions options)
    {
        Options = options;
        if (Session.IsRunning)
        {
            Session.MaskingController.Configure(options.IsMasked, options.Username, Session.CurrentPlayer.IsInGame);
        }
    }

    public void SetMasked(bool isMasked)
    {
        Options = Options with { IsMasked = isMasked };
        if (Session.IsRunning)
        {
            Session.MaskingController.SetMasked(isMasked, Session.CurrentPlayer.IsInGame);
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

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        GC.SuppressFinalize(this);
    }
}
