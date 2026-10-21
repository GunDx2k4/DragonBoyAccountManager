namespace DragonBoyAccountManager.Core.Telemetry.Snapshots;

public sealed record GameSessionSnapshot(
    PlayerSnapshot Player,
    WorldSnapshot World
);
