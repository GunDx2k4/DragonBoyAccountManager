using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Coordinators;

public readonly record struct ZoneHopOptions(
    bool ChangeZoneWhenNoMobs,
    bool CheckAllMobs,
    IReadOnlyList<string> TargetMobNames,
    bool CheckAttackByHp,
    long MinMobHp,
    long MaxMobHp);

public interface IZoneCoordinator
{
    Task<bool> TryHopZoneAsync(
        ISessionContext context,
        WorldSnapshot world,
        ZoneHopOptions options,
        string logPrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    void Reset();

    void OnMapChanged(int newMapId);
}
