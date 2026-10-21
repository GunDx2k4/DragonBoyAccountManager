using System.Collections.Concurrent;
using System.Diagnostics;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine;

public sealed class SessionManager : ISessionManager, IDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionWorker> _workers = new();
    private readonly HashSet<Guid> _userStoppedAccounts = [];
    private readonly object _userStoppedLock = new();
    private readonly SessionLauncher _launcher = new();

    public IReadOnlyCollection<Guid> ActiveSessionIds => _workers.Keys.ToList();

    public event Action<SessionEvent>? EventPublished;

    public event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? SessionUpdated;
    public event Action<Guid, string>? MessageUpdated;
    public event Action<Guid, ActionExecutionReport>? ActionReported;
    public event Action<Guid, string, int>? ChatSent;
    public event Action<Guid, string>? AutoTrainStatusChanged;
    public event Action<Guid, bool>? AutoTrainStateChanged;
    public event Action<Guid, Process>? SessionStarted;
    public event Action<Guid>? SessionExited;

    private void Publish(SessionEvent evt)
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
            case SessionProcessStartedEvent e:
                SessionStarted?.Invoke(e.AccountId, e.Process);
                break;
            case SessionProcessExitedEvent e:
                SessionExited?.Invoke(e.AccountId);
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

    public bool IsRunning(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) && worker.Session.IsRunning;
    }

    public int? GetProcessId(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) ? worker.Session.ProcessId : null;
    }

    public GameSession? GetSession(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) ? worker.Session : null;
    }

    public SessionWorker? GetWorker(Guid accountId)
    {
        return _workers.TryGetValue(accountId, out var worker) ? worker : null;
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

            GameRmsStorage.SetServerSelection(options.ServerId);

            var launchOptions = new LaunchOptions
            {
                GamePath = options.GamePath,
                SessionId = options.AccountId,
                ScreenWidth = options.ScreenWidth > 0 ? options.ScreenWidth : 1024,
                ScreenHeight = options.ScreenHeight > 0 ? options.ScreenHeight : 600,
                LogPrefix = $"DBAM_{options.Username}",
                IsMasked = options.IsMasked
            };

            var session = await _launcher.LaunchAsync(launchOptions, cancellationToken);
            var worker = new SessionWorker(session, options);

            worker.EventPublished += evt => Publish(evt);

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
            SessionUpdated?.Invoke(id, GameSessionStatus.Offline, null);
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
            SessionUpdated?.Invoke(id, GameSessionStatus.Offline, null);
        }
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
        GC.SuppressFinalize(this);
    }
}
