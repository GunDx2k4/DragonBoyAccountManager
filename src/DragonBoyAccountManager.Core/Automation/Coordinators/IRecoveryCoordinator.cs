using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Coordinators;

public readonly record struct RecoveryOptions(
    bool AutoUsePean,
    int MinHpPercent,
    int MinMpPercent);

public interface IRecoveryCoordinator
{
    Task<bool> TryUsePeanAsync(
        ISessionContext context,
        PlayerSnapshot player,
        RecoveryOptions options,
        string logPrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    Task<bool> HandleChargingStateAsync(
        ISessionContext context,
        PlayerSnapshot player,
        CancellationToken cancellationToken);

    void Reset();
}
