using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public class WorldScraper(IMonoRuntime mono) : ISnapshotScraper<WorldSnapshot>
{
    private readonly IMonoRuntime _mono = mono;
    private readonly ConcurrentDictionary<int, (string Name, bool IsFlying)> _mobTemplateCache = new();
    private readonly ConcurrentDictionary<int, string> _npcTemplateCache = new();

    public WorldSnapshot Scrape() => ScrapeWorld();

    public int GetMapId() => _mono.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapId);
    public int GetZoneId() => _mono.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticZoneId);

    public string GetMapName()
    {
        string directName = _mono.ReadStaticString(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapName);
        if (!string.IsNullOrWhiteSpace(directName)) return directName;

        IntPtr mapNamesPtr = _mono.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapNames);
        if (mapNamesPtr != IntPtr.Zero)
        {
            string[] names = _mono.ReadMonoStringArray(mapNamesPtr);
            int id = GetMapId();
            if (id >= 0 && id < names.Length && !string.IsNullOrWhiteSpace(names[id]))
                return names[id];
        }
        return string.Empty;
    }

    public WorldSnapshot ScrapeWorld()
    {
        int mapId = GetMapId();
        int zoneId = GetZoneId();
        string mapName = GetMapName();

        var mobs = ScrapeMobs();
        var (bosses, players) = ScrapeCharacters();
        var npcs = ScrapeNpcs();
        var waypoints = ScrapeWaypoints();
        var groundItems = ScrapeGroundItems();
        var zones = ScrapeZones();

        return new WorldSnapshot(
            mapId,
            zoneId,
            mapName,
            mobs,
            bosses,
            players,
            npcs,
            waypoints,
            groundItems,
            zones
        );
    }

    public IReadOnlyList<WorldMobInfo> ScrapeMobs()
    {
        var result = new List<WorldMobInfo>();
        IntPtr vMobPtr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVMob);
        if (vMobPtr == IntPtr.Zero) return result;

        var pointers = CollectionHelper.ReadMyVector(_mono, vMobPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            int id = _mono.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldMobId, ptr);
            int templateId = _mono.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldTemplateId, ptr);
            long hp = _mono.ReadField<long>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldHp, ptr);
            long maxHp = _mono.ReadField<long>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldMaxHp, ptr);
            int x = _mono.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldX, ptr);
            int y = _mono.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldY, ptr);
            int status = _mono.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldStatus, ptr);
            bool isBoss = _mono.ReadField<bool>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldIsBoss, ptr);
            sbyte levelBoss = _mono.ReadField<sbyte>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldLevelBoss, ptr);

            bool isAlive = hp > 0 && status != 0 && status != 1;
            var (name, isFlying) = GetMobTemplateInfo(templateId);

            result.Add(new WorldMobInfo(
                id, templateId, name, hp, maxHp, x, y, status, isAlive, isBoss, levelBoss > 0, isFlying));
        }

        return result;
    }

    public (IReadOnlyList<WorldBossInfo> Bosses, IReadOnlyList<WorldPlayerInfo> Players) ScrapeCharacters()
    {
        var bosses = new List<WorldBossInfo>();
        var players = new List<WorldPlayerInfo>();

        IntPtr vCharPtr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVCharInMap);
        if (vCharPtr == IntPtr.Zero) return (bosses, players);

        var pointers = CollectionHelper.ReadMyVector(_mono, vCharPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            int id = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldCharId, ptr);
            string name = _mono.ReadStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, ptr);
            long hp = _mono.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHp, ptr);
            long maxHp = _mono.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHpFull, ptr);
            int x = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, ptr);
            int y = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, ptr);
            sbyte pkType = _mono.ReadField<sbyte>(GameSchema.Char.ClassName, GameSchema.Char.FieldCTypePk, ptr);
            bool isPet = _mono.ReadField<bool>(GameSchema.Char.ClassName, GameSchema.Char.FieldIsPet, ptr);
            bool isMiniPet = _mono.ReadField<bool>(GameSchema.Char.ClassName, GameSchema.Char.FieldIsMiniPet, ptr);

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
        IntPtr vNpcPtr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVNpc);
        if (vNpcPtr == IntPtr.Zero) return result;

        var pointers = CollectionHelper.ReadMyVector(_mono, vNpcPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            int id = _mono.ReadField<int>(GameSchema.Npc.ClassName, GameSchema.Npc.FieldNpcId, ptr);
            int templateId = _mono.ReadField<int>(GameSchema.Npc.ClassName, GameSchema.Npc.FieldTemplateId, ptr);
            int x = _mono.ReadField<int>(GameSchema.Npc.ClassName, GameSchema.Npc.FieldCx, ptr);
            int y = _mono.ReadField<int>(GameSchema.Npc.ClassName, GameSchema.Npc.FieldCy, ptr);

            string name = GetNpcTemplateName(templateId);
            result.Add(new WorldNpcInfo(id, name, x, y));
        }

        return result;
    }

    public IReadOnlyList<GroundItemSnapshot> ScrapeGroundItems()
    {
        var result = new List<GroundItemSnapshot>();
        IntPtr vItemMapPtr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVItemMap);
        if (vItemMapPtr == IntPtr.Zero) return result;

        var pointers = CollectionHelper.ReadMyVector(_mono, vItemMapPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            short itemMapId = _mono.ReadField<short>(GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldItemMapId, ptr);
            int x = _mono.ReadField<int>(GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldX, ptr);
            int y = _mono.ReadField<int>(GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldY, ptr);
            int playerId = _mono.ReadField<int>(GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldPlayerId, ptr);

            short templateId = -1;
            string name = "Item";
            IntPtr templatePtr = _mono.ReadFieldPtr(GameSchema.ItemMap.ClassName, GameSchema.ItemMap.FieldTemplate, ptr);
            if (templatePtr != IntPtr.Zero)
            {
                templateId = _mono.ReadField<short>(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldId, templatePtr);
                string tmplName = _mono.ReadStringField(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldName, templatePtr);
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
        IntPtr vGoPtr = _mono.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticVGo);
        if (vGoPtr == IntPtr.Zero) return result;

        var pointers = CollectionHelper.ReadMyVector(_mono, vGoPtr);
        foreach (var ptr in pointers)
        {
            if (ptr == IntPtr.Zero) continue;

            short minX = _mono.ReadField<short>(GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldMinX, ptr);
            short minY = _mono.ReadField<short>(GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldMinY, ptr);
            short maxX = _mono.ReadField<short>(GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldMaxX, ptr);
            short maxY = _mono.ReadField<short>(GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldMaxY, ptr);

            string name = string.Empty;
            IntPtr popupPtr = _mono.ReadFieldPtr(GameSchema.Waypoint.ClassName, GameSchema.Waypoint.FieldPopup, ptr);
            if (popupPtr != IntPtr.Zero)
            {
                name = _mono.ReadStringField(GameSchema.PopUp.ClassName, GameSchema.PopUp.FieldSays, popupPtr);
            }

            int centerX = (minX + maxX) / 2;
            int centerY = (minY + maxY) / 2;

            result.Add(new WorldWaypointInfo(name, centerX, centerY, minX, minY, maxX, maxY));
        }

        return result;
    }

    public IReadOnlyList<WorldZoneInfo> ScrapeZones()
    {
        var result = new List<WorldZoneInfo>();
        IntPtr gameScr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
        if (gameScr == IntPtr.Zero) return result;

        IntPtr zonesPtr = _mono.ReadFieldPtr(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldZones, gameScr);
        if (zonesPtr == IntPtr.Zero) return result;

        int[] nums = _mono.ReadMonoIntArray(zonesPtr, 100);
        if (nums.Length == 0) return result;

        for (int i = 0; i < nums.Length; i++)
        {
            result.Add(new WorldZoneInfo(i, nums[i], 15));
        }

        return result;
    }

    private (string Name, bool IsFlying) GetMobTemplateInfo(int templateId)
    {
        if (templateId < 0) return (string.Empty, false);
        if (_mobTemplateCache.TryGetValue(templateId, out var cached))
            return cached;

        IntPtr arrMobTemplatePtr = _mono.ReadStatic<IntPtr>(GameSchema.Mob.ClassName, GameSchema.Mob.StaticArrMobTemplate);
        if (arrMobTemplatePtr == IntPtr.Zero) return (string.Empty, false);

        IntPtr[] templates = _mono.ReadMonoPointerArray(arrMobTemplatePtr);
        if (templateId < templates.Length && templates[templateId] != IntPtr.Zero)
        {
            string name = _mono.ReadStringField(GameSchema.MobTemplate.ClassName, GameSchema.MobTemplate.FieldName, templates[templateId]);
            sbyte type = _mono.ReadField<sbyte>(GameSchema.MobTemplate.ClassName, GameSchema.MobTemplate.FieldType, templates[templateId]);
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

        IntPtr arrNpcTemplatePtr = _mono.ReadStatic<IntPtr>(GameSchema.Npc.ClassName, GameSchema.Npc.StaticArrNpcTemplate);
        if (arrNpcTemplatePtr == IntPtr.Zero) return string.Empty;

        IntPtr[] templates = _mono.ReadMonoPointerArray(arrNpcTemplatePtr);
        if (templateId < templates.Length && templates[templateId] != IntPtr.Zero)
        {
            string name = _mono.ReadStringField(GameSchema.NpcTemplate.ClassName, GameSchema.NpcTemplate.FieldName, templates[templateId]);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _npcTemplateCache[templateId] = name;
                return name;
            }
        }

        return string.Empty;
    }
}
