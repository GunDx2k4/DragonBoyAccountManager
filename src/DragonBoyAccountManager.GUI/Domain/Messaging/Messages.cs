using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.GUI.Domain.Messaging;

public enum AppTab
{
    AccountConfig = 0,
    MapUtilities = 1,
    Auto = 2,
    Settings = 3,
    Guide = 4,
    Feedback = 5
}

public sealed record ClearAccountSelectionMessage;

public sealed record RequestNavigateToTabMessage(AppTab Tab);

public sealed record MaskingStateChangedMessage(bool IsMasked);

public sealed record StatusMessageChangedMessage(string Message);

public sealed record SessionSnapshotUpdatedMessage(Guid AccountId, GameSessionStatus Status, GameSessionSnapshot? Snapshot);
