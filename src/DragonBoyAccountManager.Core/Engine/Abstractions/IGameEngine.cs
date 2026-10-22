using System.Diagnostics;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface IGameActionService
{
    bool IsRunning(Guid accountId);
    Task<Result> ExecuteCommandAsync(Guid accountId, IGameCommand command, CancellationToken cancellationToken = default);
    TSnapshot? GetSnapshot<TSnapshot>(Guid accountId) where TSnapshot : class;
    IReadOnlyList<GameSkillInfo> GetSkills(Guid accountId);

    void SetGameSpeed(Guid accountId, float speed);
    float GetGameSpeed(Guid accountId);

    void SendGameKeyDown(Guid accountId, GameKey key);
    void SendGameKeyUp(Guid accountId, GameKey key);
    Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120);

    void SendGameKeyDown(Guid accountId, int keyCode);
    void SendGameKeyUp(Guid accountId, int keyCode);
    Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120);

    Task<bool> AutoMoveAsync(Guid accountId, int targetX, int targetY, bool isFastTeleport = false, CancellationToken cancellationToken = default);
    Task<bool> EnterWaypointAsync(Guid accountId, int waypointX, int waypointY, bool isFastTeleport = false, CancellationToken cancellationToken = default);
}

public interface IGameAutomationService
{
    bool StartBehavior<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class;
    bool UpdateBehaviorOptions<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class;
    bool StopBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior;
    bool IsBehaviorRunning<TBehavior>(Guid accountId) where TBehavior : class, IBehavior;
    TBehavior? GetBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior;

    bool StartBehavior(Guid accountId, string behaviorId);
    bool StopBehavior(Guid accountId, string behaviorId);
    bool IsBehaviorRunning(Guid accountId, string behaviorId);
    IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId);
}

public interface IGameEngineEvents
{
    event Action<SessionEvent>? OnSessionEvent;
}

public interface IGameEngine :
    IGameActionService,
    IGameAutomationService,
    IGameEngineEvents,
    IDisposable
{
    ISessionManager Sessions { get; }

    Task<bool> StartAccountAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default);
    Task StopAccountAsync(Guid accountId);
    Task StopAllAccountsAsync();
    void StopAllAccounts();

    int? GetProcessId(Guid accountId);
    Process? GetProcess(Guid accountId);

    Task<bool> LoginAsync(Guid accountId);

    void SetMasked(Guid accountId, bool isMasked);
    void SetAllMasked(bool isMasked);
}
