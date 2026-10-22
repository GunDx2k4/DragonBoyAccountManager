using System.Diagnostics;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface ISessionManager
{
    Action<GameSession>? SessionInitializer { get; set; }
    IReadOnlyCollection<Guid> ActiveSessionIds { get; }
    bool IsRunning(Guid accountId);
    int? GetProcessId(Guid accountId);
    Process? GetProcess(Guid accountId);
    GameSession? GetSession(Guid accountId);
    SessionWorker? GetWorker(Guid accountId);

    Task<bool> StartSessionAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default);
    Task StopSessionAsync(Guid accountId);
    Task StopAllSessionsAsync();
    void StopAllSessions();

    TSnapshot? GetSnapshot<TSnapshot>(Guid accountId) where TSnapshot : class;

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

    void SetMasked(Guid accountId, bool isMasked);
    void SetAllMasked(bool isMasked);

    event Action<SessionEvent>? EventPublished;
}
