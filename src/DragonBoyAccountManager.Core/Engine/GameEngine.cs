using System.Diagnostics;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine;

public sealed class GameEngine : IGameEngine
{
    public ISessionManager Sessions { get; }

    public event Action<SessionEvent>? OnSessionEvent;

    public GameEngine(ISessionManager? sessionManager = null)
    {
        Sessions = sessionManager ?? new SessionManager();
        Sessions.EventPublished += evt => OnSessionEvent?.Invoke(evt);
    }

    public Task<bool> StartAccountAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default)
        => Sessions.StartSessionAsync(options, cancellationToken);

    public Task StopAccountAsync(Guid accountId)
        => Sessions.StopSessionAsync(accountId);

    public Task StopAllAccountsAsync()
        => Sessions.StopAllSessionsAsync();

    public void StopAllAccounts()
        => Sessions.StopAllSessions();

    public bool IsRunning(Guid accountId) => Sessions.IsRunning(accountId);

    public int? GetProcessId(Guid accountId) => Sessions.GetProcessId(accountId);

    public Process? GetProcess(Guid accountId) => Sessions.GetProcess(accountId);

    public async Task<Result> ExecuteCommandAsync(Guid accountId, IGameCommand command, CancellationToken cancellationToken = default)
    {
        var worker = Sessions.GetWorker(accountId);
        return worker != null
            ? await worker.ExecuteCommandAsync(command, cancellationToken)
            : Result.Failure("Tài khoản chưa vào game.");
    }

    public async Task<bool> LoginAsync(Guid accountId)
    {
        var worker = Sessions.GetWorker(accountId);
        return worker != null && await worker.ExecuteLoginAsync();
    }

    public async Task<bool> AutoMoveAsync(Guid accountId, int targetX, int targetY, bool isFastTeleport = false, CancellationToken cancellationToken = default)
    {
        var worker = Sessions.GetWorker(accountId);
        if (worker != null && worker.Session.IsRunning && worker.Session.CurrentPlayer.IsInGame)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(worker.Cts.Token, cancellationToken);
            var result = await worker.Session.Navigator.NavigateAsync(
                worker.Session.CurrentPlayer,
                worker.Session.CurrentMap,
                targetX,
                targetY,
                isFastTeleport,
                null,
                linkedCts.Token);

            worker.PublishSessionUpdate();
            return result.IsSuccess;
        }
        return false;
    }

    public async Task<bool> EnterWaypointAsync(Guid accountId, int waypointX, int waypointY, bool isFastTeleport = false, CancellationToken cancellationToken = default)
    {
        var worker = Sessions.GetWorker(accountId);
        if (worker == null || !worker.Session.IsRunning || !worker.Session.CurrentPlayer.IsInGame)
        {
            return false;
        }

        int startMapId = worker.Session.CurrentWorld.MapId;
        await AutoMoveAsync(accountId, waypointX, waypointY, isFastTeleport, cancellationToken);

        if (cancellationToken.IsCancellationRequested) return false;

        int currentMapId = worker.Session.CurrentWorld.MapId;
        bool isChangingMap = worker.Session.CurrentPlayer.IsChangingMap;
        if ((startMapId >= 0 && currentMapId >= 0 && currentMapId != startMapId) || isChangingMap)
        {
            return true;
        }

        await Task.Delay(100, cancellationToken);
        if (cancellationToken.IsCancellationRequested) return false;

        currentMapId = worker.Session.CurrentWorld.MapId;
        isChangingMap = worker.Session.CurrentPlayer.IsChangingMap;
        if ((startMapId >= 0 && currentMapId >= 0 && currentMapId != startMapId) || isChangingMap)
        {
            return true;
        }

        var moveRes = await ExecuteCommandAsync(accountId, new MoveCommand(waypointX, waypointY, false), cancellationToken);
        worker.PublishSessionUpdate();
        return moveRes.IsSuccess;
    }

    public TSnapshot? GetSnapshot<TSnapshot>(Guid accountId) where TSnapshot : class
        => Sessions.GetSnapshot<TSnapshot>(accountId);

    public IReadOnlyList<GameSkillInfo> GetSkills(Guid accountId)
        => GetSnapshot<PlayerSnapshot>(accountId)?.SkillList ?? [];

    public bool StartBehavior<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class
        => Sessions.StartBehavior<TBehavior, TOptions>(accountId, options);

    public bool UpdateBehaviorOptions<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class
        => Sessions.UpdateBehaviorOptions<TBehavior, TOptions>(accountId, options);

    public bool StopBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
        => Sessions.StopBehavior<TBehavior>(accountId);

    public bool IsBehaviorRunning<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
        => Sessions.IsBehaviorRunning<TBehavior>(accountId);

    public TBehavior? GetBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
        => Sessions.GetBehavior<TBehavior>(accountId);

    public bool StartBehavior(Guid accountId, string behaviorId)
        => Sessions.StartBehavior(accountId, behaviorId);

    public bool StopBehavior(Guid accountId, string behaviorId)
        => Sessions.StopBehavior(accountId, behaviorId);

    public bool IsBehaviorRunning(Guid accountId, string behaviorId)
        => Sessions.IsBehaviorRunning(accountId, behaviorId);

    public IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId)
        => Sessions.GetBehaviors(accountId);

    public void SetGameSpeed(Guid accountId, float speed)
        => Sessions.GetWorker(accountId)?.SetGameSpeed(speed);

    public float GetGameSpeed(Guid accountId)
        => Sessions.GetWorker(accountId)?.GameSpeed ?? 1.0f;

    public void SendGameKeyDown(Guid accountId, GameKey key)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyDown(session.Mono, key);
        }
    }

    public void SendGameKeyUp(Guid accountId, GameKey key)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyUp(session.Mono, key);
        }
    }

    public Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120)
    {
        var session = Sessions.GetSession(accountId);
        return session != null && session.IsRunning
            ? GameInputSender.SendKeyPressAsync(session.Mono, key, holdMs)
            : Task.CompletedTask;
    }

    public void SendGameKeyDown(Guid accountId, int keyCode)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyDown(session.Mono, keyCode);
        }
    }

    public void SendGameKeyUp(Guid accountId, int keyCode)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyUp(session.Mono, keyCode);
        }
    }

    public Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120)
    {
        var session = Sessions.GetSession(accountId);
        return session != null && session.IsRunning
            ? GameInputSender.SendKeyPressAsync(session.Mono, keyCode, holdMs)
            : Task.CompletedTask;
    }

    public void SetMasked(Guid accountId, bool isMasked)
        => Sessions.SetMasked(accountId, isMasked);

    public void SetAllMasked(bool isMasked)
        => Sessions.SetAllMasked(isMasked);

    public void Dispose()
    {
        StopAllAccounts();
        if (Sessions is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
