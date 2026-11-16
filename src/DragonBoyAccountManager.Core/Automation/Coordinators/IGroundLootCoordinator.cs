using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Coordinators;

public readonly record struct LootFilterOptions(
    bool AutoPick,
    bool PickOthersItems,
    bool PickAllMap,
    bool IsFastTeleport,
    IReadOnlySet<short>? IncludeIds = null,
    IReadOnlySet<short>? ExcludeIds = null,
    IReadOnlyList<string>? NameFilters = null);

public interface IGroundLootCoordinator
{
    Task<bool> TryPickGroundItemsAsync(
        ISessionContext context,
        PlayerSnapshot player,
        WorldSnapshot world,
        LootFilterOptions options,
        string logPrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    void Reset();

    void BlacklistItem(int itemMapId, DateTime now);
}
