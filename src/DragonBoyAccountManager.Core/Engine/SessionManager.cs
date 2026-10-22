using System.Collections.Concurrent;
using System.Diagnostics;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Engine;

public sealed class SessionManager : ISessionManager, IDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionWorker> _workers = new();
    private readonly HashSet<Guid> _userStoppedAccounts = [];
    private readonly object _userStoppedLock = new();
    private readonly SemaphoreSlim _launchGate = new(1, 1);
    private readonly SessionLauncher _launcher = new();

    public Action<GameSession>? SessionInitializer { get; set; }
    public IReadOnlyCollection<Guid> ActiveSessionIds => _workers.Keys.ToList();

    public event Action<SessionEvent>? EventPublished;

    private void Publish(SessionEvent evt)
    {
        EventPublished?.Invoke(evt);
    }

    public bool IsRunning(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.Session.IsRunning;
    }

    public int? GetProcessId(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) ? worker.Session.ProcessId : null;
    }

    public Process? GetProcess(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.Session.IsRunning ? worker.Session.Process : null;
    }

    public GameSession? GetSession(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) ? worker.Session : null;
    }

    public SessionWorker? GetWorker(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) ? worker : null;
    }

    public TSnapshot? GetSnapshot<TSnapshot>(Guid accountId) where TSnapshot : class
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.Session.IsRunning
            ? worker.Session.GetSnapshot<TSnapshot>()
            : null;
    }

    public async Task<bool> StartSessionAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.GamePath) || !File.Exists(options.GamePath))
            return false;

        await StopSessionAsync(options.AccountId);

        lock (_userStoppedLock)
        {
            _userStoppedAccounts.Remove(options.AccountId);
        }

        try
        {
            Publish(new SessionStateChangedEvent(options.AccountId, GameSessionStatus.Waiting, null));
            Publish(new SessionMessageEvent(options.AccountId, "Đang khởi tạo game client..."));

            var globalInit = SessionInitializer;
            var perAccountInit = options.ConfigureSession;
            Action<GameSession>? combinedConfigure = (globalInit, perAccountInit) switch
            {
                (not null, not null) => s => { globalInit(s); perAccountInit(s); },
                (not null, null) => globalInit,
                (null, not null) => perAccountInit,
                _ => null
            };

            var launchOptions = new LaunchOptions
            {
                GamePath = options.GamePath,
                SessionId = options.AccountId,
                ScreenWidth = options.ScreenWidth > 0 ? options.ScreenWidth : 1024,
                ScreenHeight = options.ScreenHeight > 0 ? options.ScreenHeight : 600,
                LogPrefix = $"DBAM_{options.Username}",
                IsMasked = options.IsMasked,
                ConfigureSession = combinedConfigure
            };

            GameSession session;
            await _launchGate.WaitAsync(cancellationToken);
            try
            {
                GameRmsStorage.SetServerSelection(options.ServerId);
                session = await _launcher.LaunchAsync(launchOptions, cancellationToken);
            }
            finally
            {
                _launchGate.Release();
            }

            var worker = new SessionWorker(session, options);

            worker.EventPublished += Publish;

            _workers[options.AccountId] = worker;

            session.Exited += (_, _) =>
            {
                bool userInitiated;
                lock (_userStoppedLock)
                {
                    userInitiated = _userStoppedAccounts.Contains(options.AccountId);
                }

                if (_workers.TryRemove(options.AccountId, out var removed))
                {
                    removed.Dispose();
                }

                Publish(new SessionStateChangedEvent(options.AccountId, GameSessionStatus.Offline, null));
                if (!userInitiated)
                {
                    Publish(new SessionMessageEvent(options.AccountId, "Tiến trình game đã thoát bất ngờ."));
                }

                Publish(new SessionProcessExitedEvent(options.AccountId, userInitiated));
            };

            Publish(new SessionProcessStartedEvent(options.AccountId, session.Process));

            worker.StartLoop();

            if (options.GameSpeed != 1.0f)
            {
                worker.SetGameSpeed(options.GameSpeed);
            }

            await Task.Delay(500, cancellationToken);

            if (options.AutoLogin && !string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
            {
                return await worker.ExecuteLoginAsync();
            }
            else
            {
                Publish(new SessionStateChangedEvent(options.AccountId, GameSessionStatus.Running, null));
                return true;
            }
        }
        catch (Exception ex)
        {
            Publish(new SessionStateChangedEvent(options.AccountId, GameSessionStatus.Offline, null));
            Publish(new SessionMessageEvent(options.AccountId, $"Lỗi khởi động: {ex.Message}"));
            return false;
        }
    }

    public async Task StopSessionAsync(Guid accountId)
    {
        lock (_userStoppedLock)
        {
            _userStoppedAccounts.Add(accountId);
        }

        if (_workers.TryRemove(accountId, out var worker))
        {
            await worker.StopAsync();
        }

        Publish(new SessionStateChangedEvent(accountId, GameSessionStatus.Offline, null));
    }

    public async Task StopAllSessionsAsync()
    {
        var list = _workers.ToList();
        _workers.Clear();

        lock (_userStoppedLock)
        {
            foreach (var (id, _) in list)
            {
                _userStoppedAccounts.Add(id);
            }
        }

        foreach (var (id, worker) in list)
        {
            try { await worker.StopAsync(); } catch { }
            Publish(new SessionStateChangedEvent(id, GameSessionStatus.Offline, null));
        }
    }

    public void StopAllSessions()
    {
        var list = _workers.ToList();
        _workers.Clear();

        lock (_userStoppedLock)
        {
            foreach (var (id, _) in list)
            {
                _userStoppedAccounts.Add(id);
            }
        }

        foreach (var (id, worker) in list)
        {
            try { worker.Dispose(); } catch { }
            Publish(new SessionStateChangedEvent(id, GameSessionStatus.Offline, null));
        }
    }

    public bool StartBehavior<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.StartBehavior<TBehavior, TOptions>(options);
    }

    public bool UpdateBehaviorOptions<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.UpdateBehaviorOptions<TBehavior, TOptions>(options);
    }

    public bool StopBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.StopBehavior<TBehavior>();
    }

    public bool IsBehaviorRunning<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.IsBehaviorRunning<TBehavior>();
    }

    public TBehavior? GetBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
    {
        return _workers.TryGetValue(accountId, out var worker) ? worker.Session.GetBehavior<TBehavior>() : null;
    }

    public bool StartBehavior(Guid accountId, string behaviorId)
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.StartBehavior(behaviorId);
    }

    public bool StopBehavior(Guid accountId, string behaviorId)
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.StopBehavior(behaviorId);
    }

    public bool IsBehaviorRunning(Guid accountId, string behaviorId)
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.IsBehaviorRunning(behaviorId);
    }

    public IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) ? worker.GetBehaviors() : [];
    }

    public void SetMasked(Guid accountId, bool isMasked)
    {
        if (_workers.TryGetValue(accountId, out var worker))
        {
            worker.SetMasked(isMasked);
        }
    }

    public void SetAllMasked(bool isMasked)
    {
        foreach (var worker in _workers.Values)
        {
            worker.SetMasked(isMasked);
        }
    }

    public void Dispose()
    {
        StopAllSessions();
        _launchGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
