using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public class WorldScraper(IGameRuntime runtime) : ISnapshotScraper<WorldSnapshot>
{
    private readonly IGameRuntime _runtime = runtime;
    private readonly ConcurrentDictionary<int, (string Name, bool IsFlying)> _mobTemplateCache = new();
    private readonly ConcurrentDictionary<int, string> _npcTemplateCache = new();
    private readonly ConcurrentDictionary<int, long> _mobDeathTimes = new();
    private int _cachedMapId = -1;
    private int _cachedZoneId = -1;
    private string _cachedMapName = string.Empty;
    private IReadOnlyList<WorldWaypointInfo> _cachedWaypoints = [];

    public WorldSnapshot Scrape() => ScrapeWorld();

    public int GetMapId() => _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, _runtime.Layout.TileMap.MapId);
    public int GetZoneId() => _runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, _runtime.Layout.TileMap.ZoneId);

    public string GetMapName(int mapId)
    {
        IntPtr mapNamesPtr = _runtime.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, _runtime.Layout.TileMap.MapNames);
        if (mapNamesPtr != IntPtr.Zero && mapId >= 0)
        {
            string name = _runtime.ReadStringArrayElement(mapNamesPtr, mapId);
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        if (mapId == GetMapId())
        {
            string directName = _runtime.ReadStaticString(GameSchema.TileMap.ClassName, _runtime.Layout.TileMap.MapName);
            if (!string.IsNullOrWhiteSpace(directName)) return directName;
        }

        return string.Empty;
    }

    public WorldSnapshot ScrapeWorld()
    {
        int mapId = GetMapId();
        int zoneId = GetZoneId();

        if (mapId != _cachedMapId || zoneId != _cachedZoneId || mapId < 0)
        {
            if (mapId != _cachedMapId || mapId < 0)
            {
                _cachedMapId = mapId;
                _cachedMapName = string.Empty;
                _cachedWaypoints = [];
                InvalidateGameScrZones();
            }
            _cachedZoneId = zoneId;
            _mobDeathTimes.Clear();
        }

        if (string.IsNullOrWhiteSpace(_cachedMapName) && mapId >= 0)
        {
            _cachedMapName = GetMapName(mapId);
        }

        if (mapId >= 0)
        {
            bool shouldScrape = _cachedWaypoints.Count == 0 || _cachedWaypoints.Any(w => string.IsNullOrWhiteSpace(w.Name));

            if (!shouldScrape)
            {
                IntPtr vGoPtr = _runtime.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, _runtime.Layout.TileMap.VGo);
                if (vGoPtr != IntPtr.Zero)
                {
                    int liveCount = CollectionHelper.ReadMyVector(_runtime, vGoPtr).Count;
                    if (liveCount != _cachedWaypoints.Count)
                    {
                        shouldScrape = true;
                    }
                }
            }

            if (shouldScrape)
            {
                _cachedWaypoints = ScrapeWaypoints();
            }
        }

        var mobs = ScrapeMobs();
        var (bosses, players) = ScrapeCharacters();
        var npcs = ScrapeNpcs();
        var groundItems = ScrapeGroundItems();
        var zones = ScrapeZones();

        return new WorldSnapshot(
            mapId,
            zoneId,
            _cachedMapName,
            mobs,
            bosses,
            players,
            npcs,
            _cachedWaypoints,
            groundItems,
            zones
        );
    }

    public IReadOnlyList<WorldMobInfo> ScrapeMobs()
    {
        var result = new List<WorldMobInfo>();
        IntPtr vMobPtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.VMob);
        if (vMobPtr == IntPtr.Zero) return result;

        var mobLayout = _runtime.Layout.Mob;
        long nowMs = Environment.TickCount64;
        var pointers = CollectionHelper.ReadMyVector(_runtime, vMobPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;
            if (mobLayout.IsMobMe >= 0 && _runtime.ReadField<bool>(ptr, mobLayout.IsMobMe)) continue;

            int id = _runtime.ReadField<int>(ptr, mobLayout.MobId);
            int templateId = _runtime.ReadField<int>(ptr, mobLayout.TemplateId);
            long hp = _runtime.ReadField<long>(ptr, mobLayout.Hp);
            long maxHp = _runtime.ReadField<long>(ptr, mobLayout.MaxHp);
            int x = _runtime.ReadField<int>(ptr, mobLayout.X);
            int y = _runtime.ReadField<int>(ptr, mobLayout.Y);
            int xFirst = mobLayout.XFirst >= 0 ? _runtime.ReadField<int>(ptr, mobLayout.XFirst) : x;
            int yFirst = mobLayout.YFirst >= 0 ? _runtime.ReadField<int>(ptr, mobLayout.YFirst) : y;
            int status = _runtime.ReadField<int>(ptr, mobLayout.Status);
            bool isBoss = _runtime.ReadField<bool>(ptr, mobLayout.IsBoss);
            sbyte levelBoss = _runtime.ReadField<sbyte>(ptr, mobLayout.LevelBoss);

            bool isAlive = hp > 0 && status != 0 && status != 1;
            long deathTimestampMs = 0;
            if (isAlive)
            {
                _mobDeathTimes.TryRemove(id, out _);
            }
            else
            {
                long initialDeathTime = status == 0 ? nowMs - 1500 : nowMs;
                deathTimestampMs = _mobDeathTimes.GetOrAdd(id, initialDeathTime);
                if (x == 0 && y == 0)
                {
                    x = xFirst;
                    y = yFirst;
                }
            }

            var (name, isFlying) = GetMobTemplateInfo(templateId);

            result.Add(new WorldMobInfo(
                id, templateId, name, hp, maxHp, x, y, status, isAlive, isBoss, levelBoss > 0, isFlying, xFirst, yFirst, deathTimestampMs));
        }

        return result;
    }

    public (IReadOnlyList<WorldBossInfo> Bosses, IReadOnlyList<WorldPlayerInfo> Players) ScrapeCharacters()
    {
        var bosses = new List<WorldBossInfo>();
        var players = new List<WorldPlayerInfo>();

        IntPtr vCharPtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.VCharInMap);
        if (vCharPtr == IntPtr.Zero) return (bosses, players);

        var charLayout = _runtime.Layout.Char;
        var pointers = CollectionHelper.ReadMyVector(_runtime, vCharPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            int id = _runtime.ReadField<int>(ptr, charLayout.CharId);
            string name = _runtime.ReadStringField(ptr, charLayout.Name);
            long hp = _runtime.ReadField<long>(ptr, charLayout.Hp);
            long maxHp = _runtime.ReadField<long>(ptr, charLayout.HpFull);
            int x = _runtime.ReadField<int>(ptr, charLayout.X);
            int y = _runtime.ReadField<int>(ptr, charLayout.Y);
            sbyte pkType = _runtime.ReadField<sbyte>(ptr, charLayout.CTypePk);
            bool isPet = _runtime.ReadField<bool>(ptr, charLayout.IsPet);
            bool isMiniPet = _runtime.ReadField<bool>(ptr, charLayout.IsMiniPet);

            bool isDisciple = isPet || isMiniPet || name.StartsWith("$") || name.StartsWith("#") || name.Contains("Đệ tử");
            bool isBoss = !isDisciple && (pkType == 5 || (id < 0 && pkType != 0));

            if (isBoss)
            {
                bosses.Add(new WorldBossInfo(id, name, hp, maxHp, x, y));
            }
            else
            {
                players.Add(new WorldPlayerInfo(id, name, hp, maxHp, x, y));
            }
        }

        return (bosses, players);
    }

    public IReadOnlyList<WorldNpcInfo> ScrapeNpcs()
    {
        var result = new List<WorldNpcInfo>();
        IntPtr vNpcPtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.VNpc);
        if (vNpcPtr == IntPtr.Zero) return result;

        var npcLayout = _runtime.Layout.Npc;
        var pointers = CollectionHelper.ReadMyVector(_runtime, vNpcPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            int id = _runtime.ReadField<int>(ptr, npcLayout.NpcId);
            IntPtr templatePtr = _runtime.ReadField<IntPtr>(ptr, npcLayout.Template);
            int templateId = -1;
            string name = string.Empty;
            if (templatePtr != IntPtr.Zero)
            {
                templateId = _runtime.ReadField<int>(templatePtr, _runtime.Layout.NpcTemplate.NpcTemplateId);
                name = _runtime.ReadStringField(templatePtr, _runtime.Layout.NpcTemplate.Name);
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                name = GetNpcTemplateName(templateId);
            }

            int x = _runtime.ReadField<int>(ptr, npcLayout.Cx);
            int y = _runtime.ReadField<int>(ptr, npcLayout.Cy);

            result.Add(new WorldNpcInfo(id, name, x, y, templateId));
        }

        return result;
    }

    public IReadOnlyList<GroundItemSnapshot> ScrapeGroundItems()
    {
        var result = new List<GroundItemSnapshot>();
        IntPtr vItemMapPtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.VItemMap);
        if (vItemMapPtr == IntPtr.Zero) return result;

        var itemMapLayout = _runtime.Layout.ItemMap;
        var itemTemplateLayout = _runtime.Layout.ItemTemplate;

        var pointers = CollectionHelper.ReadMyVector(_runtime, vItemMapPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            short itemMapId = _runtime.ReadField<short>(ptr, itemMapLayout.ItemMapId);
            int x = _runtime.ReadField<int>(ptr, itemMapLayout.X);
            int y = _runtime.ReadField<int>(ptr, itemMapLayout.Y);
            int playerId = _runtime.ReadField<int>(ptr, itemMapLayout.PlayerId);

            short templateId = -1;
            string name = "Item";
            IntPtr templatePtr = _runtime.ReadFieldPtr(ptr, itemMapLayout.Template);
            if (templatePtr != IntPtr.Zero)
            {
                templateId = _runtime.ReadField<short>(templatePtr, itemTemplateLayout.Id);
                string tmplName = _runtime.ReadStringField(templatePtr, itemTemplateLayout.Name);
                if (!string.IsNullOrWhiteSpace(tmplName))
                    name = tmplName;
            }

            result.Add(new GroundItemSnapshot(itemMapId, templateId, name, x, y, playerId));
        }

        return result;
    }

    public IReadOnlyList<WorldWaypointInfo> ScrapeWaypoints()
    {
        var result = new List<WorldWaypointInfo>();
        IntPtr vGoPtr = _runtime.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, _runtime.Layout.TileMap.VGo);
        if (vGoPtr == IntPtr.Zero) return result;

        var waypointLayout = _runtime.Layout.Waypoint;
        var popUpLayout = _runtime.Layout.PopUp;

        var pointers = CollectionHelper.ReadMyVector(_runtime, vGoPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            short minX = _runtime.ReadField<short>(ptr, waypointLayout.MinX);
            short minY = _runtime.ReadField<short>(ptr, waypointLayout.MinY);
            short maxX = _runtime.ReadField<short>(ptr, waypointLayout.MaxX);
            short maxY = _runtime.ReadField<short>(ptr, waypointLayout.MaxY);
            bool isEnter = waypointLayout.IsEnter >= 0 && _runtime.ReadField<bool>(ptr, waypointLayout.IsEnter);
            bool isOffline = waypointLayout.IsOffline >= 0 && _runtime.ReadField<bool>(ptr, waypointLayout.IsOffline);

            string name = string.Empty;
            IntPtr popupPtr = _runtime.ReadFieldPtr(ptr, waypointLayout.Popup);
            if (popupPtr != IntPtr.Zero)
            {
                IntPtr saysPtr = _runtime.ReadFieldPtr(popupPtr, popUpLayout.Says);
                if (saysPtr != IntPtr.Zero)
                {
                    string[] lines = _runtime.ReadStringArray(saysPtr);
                    if (lines.Length > 0)
                    {
                        name = string.Join(" ", lines.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
                    }
                }
            }

            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;

            result.Add(new WorldWaypointInfo(name, centerX, centerY, minX, minY, maxX, maxY, isOffline, isEnter));
        }

        result.Sort((a, b) => a.MinX.CompareTo(b.MinX));
        return result;
    }

    private void InvalidateGameScrZones()
    {
        if (_runtime.Layout.GameScr.Zones < 0) return;
        IntPtr gameScr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.Instance);
        if (gameScr == IntPtr.Zero) return;
        _runtime.WriteField(gameScr, _runtime.Layout.GameScr.Zones, IntPtr.Zero);
        if (_runtime.Layout.GameScr.Pts >= 0) _runtime.WriteField(gameScr, _runtime.Layout.GameScr.Pts, IntPtr.Zero);
        if (_runtime.Layout.GameScr.NumPlayer >= 0) _runtime.WriteField(gameScr, _runtime.Layout.GameScr.NumPlayer, IntPtr.Zero);
        if (_runtime.Layout.GameScr.MaxPlayer >= 0) _runtime.WriteField(gameScr, _runtime.Layout.GameScr.MaxPlayer, IntPtr.Zero);
    }

    public IReadOnlyList<WorldZoneInfo> ScrapeZones()
    {
        var result = new List<WorldZoneInfo>();
        if (_runtime.Layout.GameScr.Zones < 0) return result;

        IntPtr gameScr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.Instance);
        if (gameScr == IntPtr.Zero) return result;

        IntPtr zonesPtr = _runtime.ReadFieldPtr(gameScr, _runtime.Layout.GameScr.Zones);
        if (zonesPtr == IntPtr.Zero) return result;

        int[] zoneIds = _runtime.ReadIntArray(zonesPtr, 100);
        if (zoneIds.Length == 0) return result;

        IntPtr ptsPtr = _runtime.Layout.GameScr.Pts >= 0
            ? _runtime.ReadFieldPtr(gameScr, _runtime.Layout.GameScr.Pts)
            : IntPtr.Zero;
        IntPtr numPlayerPtr = _runtime.Layout.GameScr.NumPlayer >= 0
            ? _runtime.ReadFieldPtr(gameScr, _runtime.Layout.GameScr.NumPlayer)
            : IntPtr.Zero;
        IntPtr maxPlayerPtr = _runtime.Layout.GameScr.MaxPlayer >= 0
            ? _runtime.ReadFieldPtr(gameScr, _runtime.Layout.GameScr.MaxPlayer)
            : IntPtr.Zero;

        int[] pts = ptsPtr != IntPtr.Zero ? _runtime.ReadIntArray(ptsPtr, zoneIds.Length) : [];
        int[] numPlayers = numPlayerPtr != IntPtr.Zero ? _runtime.ReadIntArray(numPlayerPtr, zoneIds.Length) : [];
        int[] maxPlayers = maxPlayerPtr != IntPtr.Zero ? _runtime.ReadIntArray(maxPlayerPtr, zoneIds.Length) : [];

        for (int i = 0; i < zoneIds.Length; i++)
        {
            if (zoneIds[i] < 0) continue;
            int zoneId = zoneIds[i];
            int num = i < numPlayers.Length ? numPlayers[i] : 0;
            int max = i < maxPlayers.Length && maxPlayers[i] > 0 ? maxPlayers[i] : 15;
            int status = i < pts.Length ? pts[i] : 0;
            result.Add(new WorldZoneInfo(zoneId, num, max, status));
        }

        return result;
    }

    private (string Name, bool IsFlying) GetMobTemplateInfo(int templateId)
    {
        if (templateId < 0) return (string.Empty, false);
        if (_mobTemplateCache.TryGetValue(templateId, out var cached))
            return cached;

        IntPtr arrMobTemplatePtr = _runtime.ReadStatic<IntPtr>(GameSchema.Mob.ClassName, _runtime.Layout.Mob.ArrMobTemplate);
        if (arrMobTemplatePtr == IntPtr.Zero) return (string.Empty, false);

        IntPtr[] templates = _runtime.ReadPointerArray(arrMobTemplatePtr);
        if (templateId < templates.Length && templates[templateId] != IntPtr.Zero)
        {
            string name = _runtime.ReadStringField(templates[templateId], _runtime.Layout.MobTemplate.Name);
            sbyte type = _runtime.ReadField<sbyte>(templates[templateId], _runtime.Layout.MobTemplate.Type);
            bool isFlying = type == 4 || type == 5;
            var info = (name ?? string.Empty, isFlying);
            _mobTemplateCache[templateId] = info;
            return info;
        }

        return (string.Empty, false);
    }

    private string GetNpcTemplateName(int templateId)
    {
        if (templateId < 0) return string.Empty;
        if (_npcTemplateCache.TryGetValue(templateId, out var cached))
            return cached;

        IntPtr arrNpcTemplatePtr = _runtime.ReadStatic<IntPtr>(GameSchema.Npc.ClassName, _runtime.Layout.Npc.ArrNpcTemplate);
        if (arrNpcTemplatePtr == IntPtr.Zero) return string.Empty;

        IntPtr[] templates = _runtime.ReadPointerArray(arrNpcTemplatePtr);
        if (templateId < templates.Length && templates[templateId] != IntPtr.Zero)
        {
            string name = _runtime.ReadStringField(templates[templateId], _runtime.Layout.NpcTemplate.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _npcTemplateCache[templateId] = name;
                return name;
            }
        }

        return string.Empty;
    }
}
