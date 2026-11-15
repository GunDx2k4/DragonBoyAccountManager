using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public class MapScraper(IGameRuntime runtime) : ISnapshotScraper<MapSnapshot>
{
    private readonly IGameRuntime _runtime = runtime;
    private readonly object _cacheLock = new();
    private int _cachedMapId = -1;
    private MapSnapshot? _cachedSnapshot;

    public MapSnapshot Scrape() => ScrapeMap();

    public MapSnapshot ScrapeMap(bool forceRefresh = false)
    {
        var tileMapLayout = _runtime.Layout.TileMap;
        int mapId = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.MapId);
        int zoneId = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.ZoneId);
        int tmw = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.Tmw);
        int tmh = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.Tmh);

        bool isLoading = IsMapLoading();
        if (isLoading)
        {
            lock (_cacheLock)
            {
                if (_cachedMapId != mapId)
                {
                    InvalidateCacheLocked();
                }
            }
            return MapSnapshot.Empty;
        }

        if (mapId < 0 || tmw <= 0 || tmh <= 0)
        {
            lock (_cacheLock)
            {
                InvalidateCacheLocked();
            }
            return MapSnapshot.Empty;
        }

        long totalTiles = (long)tmw * tmh;
        if (totalTiles <= 0 || totalTiles > 1_000_000)
        {
            lock (_cacheLock)
            {
                InvalidateCacheLocked();
            }
            return MapSnapshot.Empty;
        }

        if (!forceRefresh)
        {
            lock (_cacheLock)
            {
                if (_cachedMapId == mapId &&
                    _cachedSnapshot != null &&
                    _cachedSnapshot.IsValid &&
                    _cachedSnapshot.Width == tmw &&
                    _cachedSnapshot.Height == tmh)
                {
                    if (_cachedSnapshot.ZoneId != zoneId)
                    {
                        _cachedSnapshot = _cachedSnapshot with { ZoneId = zoneId };
                    }
                    return _cachedSnapshot;
                }
            }
        }

        IntPtr typesPtr = _runtime.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, tileMapLayout.Types);
        if (typesPtr == IntPtr.Zero)
            return MapSnapshot.Empty;

        int expectedLength = (int)totalTiles;
        if (_runtime.ReadArrayLength(typesPtr) < expectedLength)
            return MapSnapshot.Empty;

        int[] types = _runtime.ReadIntArray(typesPtr, expectedLength);
        if (types.Length < expectedLength || !HasCollisionData(types))
            return MapSnapshot.Empty;

        int postMapId = _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, tileMapLayout.MapId);
        if (postMapId != mapId || IsMapLoading())
            return MapSnapshot.Empty;

        string mapName = _runtime.ReadStaticString(GameSchema.TileMap.ClassName, tileMapLayout.MapName);
        var snapshot = new MapSnapshot(mapId, zoneId, mapName, tmw, tmh, types);
        lock (_cacheLock)
        {
            _cachedMapId = mapId;
            _cachedSnapshot = snapshot;
        }
        return snapshot;
    }

    private bool IsMapLoading()
    {
        return _runtime.ReadStatic<bool>(GameSchema.Char.ClassName, _runtime.Layout.Char.IsLoadingMap) ||
               _runtime.ReadStatic<bool>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.IsLoading) ||
               _runtime.ReadStatic<bool>(GameSchema.Char.ClassName, _runtime.Layout.Char.IsChangingMap) ||
               (_runtime.Layout.Controller.IsStopReadMessage >= 0 &&
                _runtime.ReadStatic<bool>(GameSchema.Controller.ClassName, _runtime.Layout.Controller.IsStopReadMessage));
    }

    private void InvalidateCacheLocked()
    {
        _cachedMapId = -1;
        _cachedSnapshot = null;
    }

    private static bool HasCollisionData(int[] types)
    {
        for (int i = 0; i < types.Length; i++)
        {
            if (types[i] != 0)
                return true;
        }
        return false;
    }
}
