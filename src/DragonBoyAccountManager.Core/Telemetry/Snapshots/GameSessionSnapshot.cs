namespace DragonBoyAccountManager.Core.Telemetry.Snapshots;

public sealed record GameSessionSnapshot(
    PlayerSnapshot Player,
    WorldSnapshot World,
    IReadOnlyDictionary<Type, object>? CustomSnapshots = null)
{
    public MapSnapshot Map => Get<MapSnapshot>() ?? MapSnapshot.Empty;

    public TSnapshot? Get<TSnapshot>() where TSnapshot : class
    {
        if (typeof(TSnapshot) == typeof(PlayerSnapshot)) return Player as TSnapshot;
        if (typeof(TSnapshot) == typeof(WorldSnapshot)) return World as TSnapshot;
        if (CustomSnapshots != null && CustomSnapshots.TryGetValue(typeof(TSnapshot), out var snap))
        {
            return snap as TSnapshot;
        }
        return null;
    }
}
