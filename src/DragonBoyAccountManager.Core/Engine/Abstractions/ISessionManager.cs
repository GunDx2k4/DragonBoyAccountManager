using System.Diagnostics;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface ISessionManager
{
    IReadOnlyCollection<Guid> ActiveSessionIds { get; }
    bool IsRunning(Guid accountId);
    int? GetProcessId(Guid accountId);
    GameSession? GetSession(Guid accountId);
    SessionWorker? GetWorker(Guid accountId);

    Task<bool> StartSessionAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default);
    Task StopSessionAsync(Guid accountId);
    Task StopAllSessionsAsync();
    void StopAllSessions();

    bool StartBehavior(Guid accountId, string behaviorId);
    bool StopBehavior(Guid accountId, string behaviorId);
    bool IsBehaviorRunning(Guid accountId, string behaviorId);
    IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId);

    event Action<SessionEvent>? EventPublished;

    event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? SessionUpdated;
    event Action<Guid, string>? MessageUpdated;
    event Action<Guid, ActionExecutionReport>? ActionReported;
    event Action<Guid, string, int>? ChatSent;
    event Action<Guid, string>? AutoTrainStatusChanged;
    event Action<Guid, bool>? AutoTrainStateChanged;
    event Action<Guid, Process>? SessionStarted;
    event Action<Guid>? SessionExited;
}
