using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public class MapScraper(IGameRuntime runtime) : ISnapshotScraper<MapSnapshot>
{
    private readonly IGameRuntime _runtime = runtime;
    private int _cachedMapId = -1;
    private MapSnapshot? _cachedSnapshot;

    public MapSnapshot Scrape() => ScrapeMap();

    public MapSnapshot ScrapeMap(bool forceRefresh = false)
    {
        var tileMapLayout = _runtime.Layout.TileMap;
        int mapId = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.MapId);
        int zoneId = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.ZoneId);

        if (!forceRefresh && _cachedMapId == mapId && _cachedSnapshot != null && _cachedSnapshot.IsValid)
        {
            if (_cachedSnapshot.ZoneId != zoneId)
            {
                _cachedSnapshot = _cachedSnapshot with { ZoneId = zoneId };
            }
            return _cachedSnapshot;
        }

        int tmw = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.Tmw);
        int tmh = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.Tmh);
        string mapName = _runtime.ReadStaticString(GameSchema.TileMap.ClassName, tileMapLayout.MapName);

        if (tmw <= 0 || tmh <= 0)
            return MapSnapshot.Empty;

        IntPtr typesPtr = _runtime.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, tileMapLayout.Types);
        if (typesPtr == IntPtr.Zero)
        {
            typesPtr = _runtime.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, tileMapLayout.Maps);
        }

        if (typesPtr == IntPtr.Zero)
            return MapSnapshot.Empty;

        int[] types = _runtime.ReadIntArray(typesPtr, tmw * tmh);
        if (types.Length == 0)
            return MapSnapshot.Empty;

        if (types.Length < tmw * tmh)
        {
            Array.Resize(ref types, tmw * tmh);
        }

        var snapshot = new MapSnapshot(mapId, zoneId, mapName, tmw, tmh, types);
        _cachedMapId = mapId;
        _cachedSnapshot = snapshot;
        return snapshot;
    }
}
