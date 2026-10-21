using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public class MapScraper(IMonoRuntime mono)
{
    private readonly IMonoRuntime _mono = mono;
    private int _cachedMapId = -1;
    private MapSnapshot? _cachedSnapshot;

    public MapSnapshot ScrapeMap(bool forceRefresh = false)
    {
        int mapId = _mono.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapId);
        int zoneId = _mono.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticZoneId);

        if (!forceRefresh && _cachedMapId == mapId && _cachedSnapshot != null && _cachedSnapshot.IsValid)
        {
            if (_cachedSnapshot.ZoneId != zoneId)
            {
                _cachedSnapshot = _cachedSnapshot with { ZoneId = zoneId };
            }
            return _cachedSnapshot;
        }

        int tmw = _mono.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTmw);
        int tmh = _mono.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTmh);
        string mapName = _mono.ReadStaticString(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapName);

        if (tmw <= 0 || tmh <= 0)
            return MapSnapshot.Empty;

        IntPtr typesPtr = _mono.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTypes);
        if (typesPtr == IntPtr.Zero)
        {
            typesPtr = _mono.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMaps);
        }

        if (typesPtr == IntPtr.Zero)
            return MapSnapshot.Empty;

        int[]? types = ReadMonoIntArray(typesPtr, tmw * tmh);
        if (types == null || types.Length == 0)
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

    private int[]? ReadMonoIntArray(IntPtr monoArrayPtr, int expectedLength)
    {
        if (monoArrayPtr == IntPtr.Zero) return null;
        int length = ((MonoRuntime)_mono).Memory.ReadInt(monoArrayPtr + (((MonoRuntime)_mono).Memory.PointerSize * 3));
        if (length <= 0 || length > 1_000_000) return null;

        int actualLength = Math.Min(length, expectedLength);
        int[] result = new int[actualLength];
        int dataOffset = ((MonoRuntime)_mono).Memory.PointerSize * 4;

        for (int i = 0; i < actualLength; i++)
        {
            result[i] = ((MonoRuntime)_mono).Memory.ReadInt(monoArrayPtr + dataOffset + i * 4);
        }
        return result;
    }
}
