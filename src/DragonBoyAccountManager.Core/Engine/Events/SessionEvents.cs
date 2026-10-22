using System.Diagnostics;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine.Events;

public abstract record SessionEvent(Guid AccountId, DateTime Timestamp)
{
    protected SessionEvent(Guid accountId) : this(accountId, DateTime.UtcNow) { }
}

public sealed record SessionStateChangedEvent(
    Guid AccountId,
    GameSessionStatus Status,
    GameSessionSnapshot? Snapshot
) : SessionEvent(AccountId);

public sealed record SessionMessageEvent(
    Guid AccountId,
    string Message
) : SessionEvent(AccountId);

public sealed record SessionProcessStartedEvent(
    Guid AccountId,
    Process Process
) : SessionEvent(AccountId);

public sealed record SessionProcessExitedEvent(
    Guid AccountId,
    bool UserInitiated
) : SessionEvent(AccountId);

public sealed record BehaviorProgressEvent(
    Guid AccountId,
    string BehaviorId,
    string Message
) : SessionEvent(AccountId);

public sealed record BehaviorStateChangedEvent(
    Guid AccountId,
    string BehaviorId,
    bool IsRunning,
    string? StatusMessage = null
) : SessionEvent(AccountId);

public sealed record ActionReportedEvent(
    Guid AccountId,
    ActionExecutionReport Report
) : SessionEvent(AccountId);
