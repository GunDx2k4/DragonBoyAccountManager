using System.Collections.Concurrent;
using System.Diagnostics;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Engine;

public sealed class SessionManager : ISessionManager, IDisposable, IAsyncDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionWorker> _workers = new();
    private readonly ConcurrentDictionary<Guid, AccountLaunchOptions> _launchOptions = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _reconnectCts = new();
    private readonly HashSet<Guid> _userStoppedAccounts = [];
    private readonly Lock _userStoppedLock = new();
    private readonly SemaphoreSlim _launchGate = new(1, 1);
    private readonly CancellationTokenSource _disposeCts = new();
    private readonly ISessionLauncher _launcher;

    public Action<GameSession>? SessionInitializer { get; set; }
    public IReadOnlyCollection<Guid> ActiveSessionIds => _workers.Keys.ToList();

    public event Action<SessionEvent>? EventPublished;

    public SessionManager(ISessionLauncher? launcher = null)
    {
        _launcher = launcher ?? new SessionLauncher();
    }

    private void Publish(SessionEvent evt)
    {
        EventPublished?.Invoke(evt);
    }

    public bool IsRunning(Guid accountId)
    {
        return (_workers.TryGetValue(accountId, out var worker) && worker.Session.IsRunning)
               || _reconnectCts.ContainsKey(accountId);
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

    private void CancelReconnect(Guid accountId)
    {
        if (_reconnectCts.TryRemove(accountId, out var cts))
        {
            try { cts.Cancel(); } catch { }
        }
    }

    public async Task<bool> StartSessionAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.GamePath) || !File.Exists(options.GamePath))
            return false;

        CancelReconnect(options.AccountId);
        await StopWorkerOnlyAsync(options.AccountId);

        _launchOptions[options.AccountId] = options;

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
                ServerId = options.ServerId,
                ScreenWidth = options.ScreenWidth > 0 ? options.ScreenWidth : 1024,
                ScreenHeight = options.ScreenHeight > 0 ? options.ScreenHeight : 600,
                ZoomLevel = options.ZoomLevel,
                LogPrefix = $"DBAM_{options.Username}",
                IsMasked = options.IsMasked,
                ConfigureSession = combinedConfigure
            };

            GameSession session;
            await _launchGate.WaitAsync(cancellationToken);
            try
            {
                session = await _launcher.LaunchAsync(launchOptions, cancellationToken);
            }
            finally
            {
                _launchGate.Release();
            }

            var worker = new SessionWorker(session, options);

            worker.EventPublished += Publish;

            _workers[options.AccountId] = worker;

            session.Exited += (_, _) => HandleSessionExited(options.AccountId);

            Publish(new SessionProcessStartedEvent(options.AccountId, session.Process));

            worker.StartLoop();

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

    private void HandleSessionExited(Guid accountId)
    {
        bool userInitiated;
        lock (_userStoppedLock)
        {
            userInitiated = _userStoppedAccounts.Contains(accountId);
        }

        if (_workers.TryRemove(accountId, out var removed))
        {
            removed.Dispose();
        }

        if (userInitiated)
        {
            CancelReconnect(accountId);
            _launchOptions.TryRemove(accountId, out _);
            Publish(new SessionStateChangedEvent(accountId, GameSessionStatus.Offline, null));
            Publish(new SessionProcessExitedEvent(accountId, true));
            return;
        }

        if (_launchOptions.TryGetValue(accountId, out var savedOptions))
        {
            var (autoReconnect, rawDelay) = savedOptions.ReconnectPolicyResolver?.Invoke()
                ?? (savedOptions.AutoReconnect, savedOptions.ReconnectDelaySeconds);

            if (autoReconnect)
            {
                int delay = Math.Max(1, rawDelay);
                string msg = $"Mất kết nối game. Tự động kết nối lại sau {delay} giây...";
                Publish(new SessionStateChangedEvent(accountId, GameSessionStatus.Waiting, null));
                Publish(new SessionMessageEvent(accountId, msg));
                Publish(new SessionProcessExitedEvent(accountId, false));

                CancelReconnect(accountId);
                var cts = new CancellationTokenSource();
                _reconnectCts[accountId] = cts;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (_disposeCts.IsCancellationRequested || cts.IsCancellationRequested)
                        {
                            return;
                        }

                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, _disposeCts.Token);
                        await Task.Delay(TimeSpan.FromSeconds(delay), linkedCts.Token);
                        if (!linkedCts.Token.IsCancellationRequested && _launchOptions.TryGetValue(accountId, out var opts))
                        {
                            _reconnectCts.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(accountId, cts));
                            await StartSessionAsync(opts, linkedCts.Token);
                        }
                    }
                    catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
                    {
                    }
                    finally
                    {
                        _reconnectCts.TryRemove(new KeyValuePair<Guid, CancellationTokenSource>(accountId, cts));
                        cts.Dispose();
                    }
                }, CancellationToken.None);
                return;
            }
        }

        _launchOptions.TryRemove(accountId, out _);
        Publish(new SessionStateChangedEvent(accountId, GameSessionStatus.Offline, null));
        Publish(new SessionMessageEvent(accountId, "Tiến trình game đã thoát bất ngờ."));
        Publish(new SessionProcessExitedEvent(accountId, false));
    }

    private async Task StopWorkerOnlyAsync(Guid accountId)
    {
        lock (_userStoppedLock)
        {
            _userStoppedAccounts.Add(accountId);
        }

        if (_workers.TryRemove(accountId, out var worker))
        {
            await worker.StopAsync();
        }
    }

    public async Task StopSessionAsync(Guid accountId)
    {
        CancelReconnect(accountId);
        _launchOptions.TryRemove(accountId, out _);
        await StopWorkerOnlyAsync(accountId);
        Publish(new SessionStateChangedEvent(accountId, GameSessionStatus.Offline, null));
    }

    private List<(Guid Id, SessionWorker Worker)> DrainWorkersAndCancelReconnects()
    {
        foreach (var id in _reconnectCts.Keys.ToList())
        {
            CancelReconnect(id);
        }
        _launchOptions.Clear();

        var list = _workers.Select(kvp => (kvp.Key, kvp.Value)).ToList();
        _workers.Clear();

        lock (_userStoppedLock)
        {
            foreach (var (id, _) in list)
            {
                _userStoppedAccounts.Add(id);
            }
        }

        return list;
    }

    public async Task StopAllSessionsAsync()
    {
        var list = DrainWorkersAndCancelReconnects();

        foreach (var (id, worker) in list)
        {
            try { await worker.StopAsync(); } catch { }
            Publish(new SessionStateChangedEvent(id, GameSessionStatus.Offline, null));
        }
    }

    public void StopAllSessions()
    {
        var list = DrainWorkersAndCancelReconnects();

        foreach (var (id, worker) in list)
        {
            try { worker.Dispose(); } catch { }
            Publish(new SessionStateChangedEvent(id, GameSessionStatus.Offline, null));
        }
    }

    public void Dispose()
    {
        try { _disposeCts.Cancel(); } catch { }
        StopAllSessions();
        _disposeCts.Dispose();
        _launchGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        try { _disposeCts.Cancel(); } catch { }
        await StopAllSessionsAsync();
        _disposeCts.Dispose();
        _launchGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
