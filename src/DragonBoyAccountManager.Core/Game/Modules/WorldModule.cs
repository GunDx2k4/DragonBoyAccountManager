using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Game.Entities;
using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Modules;

public interface IWorldModule
{
    int MapId { get; }
    int ZoneId { get; }
    string MapName { get; }

    (int Width, int Height, int[] Types)? GetCollisionData();

    IReadOnlyList<MobEntity> GetMonsters();
    IReadOnlyList<MobEntity> GetAliveMonsters();
    MobEntity? GetNearestMonster(int? templateId = null, int fromX = -1, int fromY = -1);

    IReadOnlyList<PlayerEntity> GetPlayers();
    IReadOnlyList<NpcEntity> GetNpcs();
    IReadOnlyList<ItemMapEntity> GetGroundItems();
    ItemMapEntity? GetNearestGroundItem(short? templateId = null, int fromX = -1, int fromY = -1);

    IReadOnlyList<WaypointEntity> GetWaypoints();
    WaypointEntity? GetNearestWaypoint(int fromX = -1, int fromY = -1);
    IReadOnlyList<WorldZoneInfo> GetZones();

    bool Focus(WorldEntity entity);
    bool FocusOrClick(string entityType, int entityId);
    bool FocusOrClickObject(string entityType, int entityId);
    bool MoveToWaypoint(int targetX, int targetY);
    int GetYGround(int x);

    void CheckClickMoveTo(int x, int y);
    void DoDoubleClickToObj(IntPtr objPtr);
    bool ClickMoving { get; set; }

    bool ChangeZone(int zoneId);
    void RequestZoneList();
    void CloseZonePanel();

    WorldSnapshot GetSnapshot();
}

public class WorldModule(MonoReflector reflector, IUIModule ui, INetworkModule network) : IWorldModule
{
    private readonly MonoReflector _reflector = reflector;
    private readonly IUIModule _ui = ui;
    private readonly INetworkModule _network = network;

    private readonly ConcurrentDictionary<int, (string Name, bool IsFlying)> _mobTemplateCache = new();
    private readonly ConcurrentDictionary<int, string> _npcTemplateCache = new();

    private int _cachedCollisionMapId = -1;
    private (int Width, int Height, int[] Types)? _cachedCollisionData;

    private IntPtr GameScrPointer
    {
        get
        {
            try
            {
                return _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }

    private IntPtr ServicePointer
    {
        get
        {
            try
            {
                IntPtr ptr = _reflector.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
                if (ptr == IntPtr.Zero)
                    ptr = _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
                return ptr;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }

    public int MapId
    {
        get
        {
            try { return _reflector.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapId); }
            catch { return -1; }
        }
    }

    public int ZoneId
    {
        get
        {
            try { return _reflector.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticZoneId); }
            catch { return -1; }
        }
    }

    public string MapName
    {
        get
        {
            try
            {
                string staticName = _reflector.ReadStaticString(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapName);
                if (!string.IsNullOrWhiteSpace(staticName))
                {
                    return staticName;
                }

                IntPtr mapNamesPtr = _reflector.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMapNames);
                if (mapNamesPtr != IntPtr.Zero)
                {
                    string[] names = _reflector.ReadMonoStringArray(mapNamesPtr);
                    int id = MapId;
                    if (names != null && id >= 0 && id < names.Length && !string.IsNullOrWhiteSpace(names[id]))
                    {
                        return names[id];
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    private int Tmw => _reflector.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTmw);
    private int Tmh => _reflector.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTmh);

    private int Pxw
    {
        get
        {
            int pxw = _reflector.ReadStatic<int>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticPxw);
            if (pxw <= 0)
            {
                int tmw = Tmw;
                pxw = (tmw > 0 ? tmw : 100) * 24;
            }
            return pxw;
        }
    }

    public (int Width, int Height, int[] Types)? GetCollisionData()
    {
        try
        {
            int tmw = Tmw;
            int tmh = Tmh;
            if (tmw <= 0 || tmh <= 0) return null;

            int mapId = MapId;
            if (mapId >= 0 && _cachedCollisionMapId == mapId && _cachedCollisionData != null)
                return _cachedCollisionData;

            IntPtr typesPtr = _reflector.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticTypes);
            if (typesPtr == IntPtr.Zero)
            {
                typesPtr = _reflector.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticMaps);
            }

            if (typesPtr == IntPtr.Zero) return null;

            int[] types = _reflector.ReadMonoIntArray(typesPtr, 300_000);
            if (types == null || types.Length == 0) return null;

            if (types.Length < tmw * tmh)
            {
                Array.Resize(ref types, tmw * tmh);
            }

            if (mapId >= 0)
            {
                _cachedCollisionMapId = mapId;
                _cachedCollisionData = (tmw, tmh, types);
            }

            return (tmw, tmh, types);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<MobEntity> GetMonsters()
    {
        var result = new List<MobEntity>();
        if (!_ui.IsGamePlayScreen) return result;

        try
        {
            IntPtr vMobPtr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVMob);
            if (vMobPtr == IntPtr.Zero) return result;

            var pointers = _reflector.ReadMyVector(vMobPtr);
            foreach (var ptr in pointers)
            {
                if (ptr == IntPtr.Zero) continue;
                var mob = new MobEntity(_reflector, ptr);
                var info = GetMobTemplateInfo(mob.TemplateId);
                mob.Name = info.Name;
                mob.IsFlying = info.IsFlying;
                result.Add(mob);
            }
        }
        catch { }

        return result;
    }

    public IReadOnlyList<MobEntity> GetAliveMonsters() => GetMonsters().Where(m => m.IsAlive).ToList();

    public MobEntity? GetNearestMonster(int? templateId = null, int fromX = -1, int fromY = -1)
    {
        var mobs = GetAliveMonsters();
        if (templateId.HasValue)
        {
            mobs = mobs.Where(m => m.TemplateId == templateId.Value).ToList();
        }

        if (mobs.Count == 0) return null;
        if (fromX < 0 || fromY < 0) return mobs[0];

        return mobs.OrderBy(m => m.DistanceTo(fromX, fromY)).FirstOrDefault();
    }

    public IReadOnlyList<PlayerEntity> GetPlayers()
    {
        var result = new List<PlayerEntity>();
        if (!_ui.IsGamePlayScreen) return result;

        try
        {
            IntPtr vCharPtr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVCharInMap);
            if (vCharPtr == IntPtr.Zero) return result;

            var pointers = _reflector.ReadMyVector(vCharPtr);
            foreach (var ptr in pointers)
            {
                if (ptr == IntPtr.Zero) continue;
                result.Add(new PlayerEntity(_reflector, ptr));
            }
        }
        catch { }

        return result;
    }

    public IReadOnlyList<NpcEntity> GetNpcs()
    {
        var result = new List<NpcEntity>();
        if (!_ui.IsGamePlayScreen) return result;

        try
        {
            IntPtr vNpcPtr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVNpc);
            if (vNpcPtr == IntPtr.Zero) return result;

            var pointers = _reflector.ReadMyVector(vNpcPtr);
            foreach (var ptr in pointers)
            {
                if (ptr == IntPtr.Zero) continue;
                var npc = new NpcEntity(_reflector, ptr);
                if (string.IsNullOrEmpty(npc.Name))
                {
                    npc.Name = GetNpcTemplateName(npc.TemplateId);
                }
                result.Add(npc);
            }
        }
        catch { }

        return result;
    }

    public IReadOnlyList<ItemMapEntity> GetGroundItems()
    {
        var result = new List<ItemMapEntity>();
        if (!_ui.IsGamePlayScreen) return result;

        try
        {
            IntPtr vItemMapPtr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVItemMap);
            if (vItemMapPtr == IntPtr.Zero) return result;

            var pointers = _reflector.ReadMyVector(vItemMapPtr);
            foreach (var ptr in pointers)
            {
                if (ptr == IntPtr.Zero) continue;
                result.Add(new ItemMapEntity(_reflector, ptr));
            }
        }
        catch { }

        return result;
    }

    public ItemMapEntity? GetNearestGroundItem(short? templateId = null, int fromX = -1, int fromY = -1)
    {
        var items = GetGroundItems();
        if (templateId.HasValue)
        {
            items = items.Where(i => i.TemplateId == templateId.Value).ToList();
        }

        if (items.Count == 0) return null;
        if (fromX < 0 || fromY < 0) return items[0];

        return items.OrderBy(i => i.DistanceTo(fromX, fromY)).FirstOrDefault();
    }

    public IReadOnlyList<WaypointEntity> GetWaypoints()
    {
        var result = new List<WaypointEntity>();
        try
        {
            IntPtr vGoPtr = _reflector.ReadStatic<IntPtr>(GameSchema.TileMap.ClassName, GameSchema.TileMap.StaticVGo);
            if (vGoPtr == IntPtr.Zero) return result;

            var pointers = _reflector.ReadMyVector(vGoPtr);
            int pxw = Pxw;

            foreach (var ptr in pointers)
            {
                if (ptr == IntPtr.Zero) continue;
                result.Add(new WaypointEntity(_reflector, ptr, pxw));
            }
        }
        catch { }

        return result;
    }

    public WaypointEntity? GetNearestWaypoint(int fromX = -1, int fromY = -1)
    {
        var wps = GetWaypoints();
        if (wps.Count == 0) return null;
        if (fromX < 0 || fromY < 0) return wps[0];

        return wps.OrderBy(w => w.DistanceTo(fromX, fromY)).FirstOrDefault();
    }

    public IReadOnlyList<WorldZoneInfo> GetZones()
    {
        var zones = new List<WorldZoneInfo>();
        if (!_ui.IsGamePlayScreen) return zones;

        if (_ui.IsZonePanelOpen)
        {
            _ui.CloseZonePanel();
        }

        try
        {
            IntPtr gameScr = GameScrPointer;
            if (gameScr == IntPtr.Zero) return zones;

            IntPtr zonesArrPtr = _reflector.ReadFieldPtr(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldZones, gameScr);
            IntPtr numPlayerArrPtr = _reflector.ReadFieldPtr(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldNumPlayer, gameScr);
            IntPtr maxPlayerArrPtr = _reflector.ReadFieldPtr(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldMaxPlayer, gameScr);
            IntPtr ptsArrPtr = _reflector.ReadFieldPtr(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldPts, gameScr);

            if (zonesArrPtr != IntPtr.Zero)
            {
                int[] zoneIds = _reflector.ReadMonoIntArray(zonesArrPtr, 50);
                int[] numPlayers = numPlayerArrPtr != IntPtr.Zero ? _reflector.ReadMonoIntArray(numPlayerArrPtr, 50) : [];
                int[] maxPlayers = maxPlayerArrPtr != IntPtr.Zero ? _reflector.ReadMonoIntArray(maxPlayerArrPtr, 50) : [];
                int[] pts = ptsArrPtr != IntPtr.Zero ? _reflector.ReadMonoIntArray(ptsArrPtr, 50) : [];

                for (int i = 0; i < zoneIds.Length; i++)
                {
                    int zoneId = zoneIds[i];
                    int count = (i < numPlayers.Length) ? numPlayers[i] : 0;
                    int max = (i < maxPlayers.Length && maxPlayers[i] > 0) ? maxPlayers[i] : 15;
                    int status = (i < pts.Length) ? pts[i] : 0;
                    zones.Add(new WorldZoneInfo(zoneId, count, max, status));
                }
            }
        }
        catch { }

        return zones;
    }

    public bool Focus(WorldEntity entity)
    {
        if (entity == null || !entity.IsAlive) return false;

        try
        {
            IntPtr myCharPtr = _reflector.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
            if (myCharPtr == IntPtr.Zero) return false;

            if (entity is MobEntity)
            {
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldMobFocus, myCharPtr, entity.Pointer);
                _reflector.CallMethod(GameSchema.Char.ClassName, GameSchema.Char.MethodFocusManualTo, myCharPtr, entity.Pointer);
                return true;
            }
            if (entity is PlayerEntity)
            {
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCharFocus, myCharPtr, entity.Pointer);
                _reflector.CallMethod(GameSchema.Char.ClassName, GameSchema.Char.MethodFocusManualTo, myCharPtr, entity.Pointer);
                return true;
            }
            if (entity is NpcEntity)
            {
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldNpcFocus, myCharPtr, entity.Pointer);
                _reflector.CallMethod(GameSchema.Char.ClassName, GameSchema.Char.MethodFocusManualTo, myCharPtr, entity.Pointer);
                return true;
            }
            if (entity is ItemMapEntity)
            {
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldItemFocus, myCharPtr, entity.Pointer);
                _reflector.CallMethod(GameSchema.Char.ClassName, GameSchema.Char.MethodFocusManualTo, myCharPtr, entity.Pointer);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool FocusOrClick(string entityType, int entityId)
    {
        try
        {
            IntPtr myCharPtr = _reflector.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
            if (myCharPtr == IntPtr.Zero) return false;

            string actualType = entityType?.ToLowerInvariant() ?? "";
            if (actualType is "mob" or "mobentity")
            {
                var mob = GetMonsters().FirstOrDefault(m => m.Id == entityId);
                if (mob != null && mob.IsAlive)
                {
                    _reflector.CallMethod(GameSchema.Char.ClassName, GameSchema.Char.MethodFocusManualTo, myCharPtr, 0, mob.Pointer);
                    return true;
                }
            }
            else if (actualType is "char" or "player" or "playerentity")
            {
                var p = GetPlayers().FirstOrDefault(c => c.Id == entityId);
                if (p != null && p.IsAlive)
                {
                    _reflector.CallMethod(GameSchema.Char.ClassName, GameSchema.Char.MethodFocusManualTo, myCharPtr, 1, p.Pointer);
                    return true;
                }
            }
            else if (actualType is "npc" or "npcentity")
            {
                var npc = GetNpcs().FirstOrDefault(n => n.Id == entityId);
                if (npc != null)
                {
                    _reflector.CallMethod(GameSchema.Char.ClassName, GameSchema.Char.MethodFocusManualTo, myCharPtr, 2, npc.Pointer);
                    DoDoubleClickToObj(npc.Pointer);
                    return true;
                }
            }
            else if (actualType is "itemmap" or "itemmapentity")
            {
                var item = GetGroundItems().FirstOrDefault(i => i.Id == entityId);
                if (item != null)
                {
                    _reflector.CallMethod(GameSchema.Char.ClassName, GameSchema.Char.MethodFocusManualTo, myCharPtr, 3, item.Pointer);
                    DoDoubleClickToObj(item.Pointer);
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public bool FocusOrClickObject(string entityType, int entityId) => FocusOrClick(entityType, entityId);

    public bool MoveToWaypoint(int targetX, int targetY)
    {
        try
        {
            bool isChanging = _reflector.ReadStatic<bool>(GameSchema.Char.ClassName, GameSchema.Char.StaticIsChangingMap);
            bool isLoading = _reflector.ReadStatic<bool>(GameSchema.Char.ClassName, GameSchema.Char.StaticIsLoadingMap);
            if (isChanging || isLoading) return false;

            var waypoints = GetWaypoints();
            if (waypoints.Count == 0) return false;

            var bestWp = waypoints.OrderBy(wp => wp.DistanceTo(targetX, targetY)).FirstOrDefault();
            if (bestWp != null)
            {
                IntPtr myCharPtr = _reflector.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
                if (myCharPtr != IntPtr.Zero)
                {
                    int curX = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
                    int curY = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);
                    double distToChar = bestWp.DistanceTo(curX, curY);
                    double distToTarget = bestWp.DistanceTo(targetX, targetY);

                    if (distToChar > 100 && distToTarget > 100)
                    {
                        return false;
                    }
                }

                if (bestWp.IsOffline)
                {
                    _network.GetMapOffline();
                }
                else
                {
                    _network.RequestChangeMap();
                }
                return true;
            }
        }
        catch { }

        return false;
    }

    public int GetYGround(int x)
    {
        var col = GetCollisionData();
        if (col.HasValue)
        {
            int tmw = col.Value.Width;
            int tmh = col.Value.Height;
            int[] types = col.Value.Types;

            int tx = Math.Clamp(x / 24, 0, tmw - 1);
            int y = 50;
            for (int i = 0; i < 30; i++)
            {
                y += 24;
                int ty = y / 24;
                if (ty >= tmh) break;
                int idx = ty * tmw + tx;
                if (idx >= 0 && idx < types.Length && (types[idx] & 2) == 2)
                {
                    if (y % 24 != 0) y -= y % 24;
                    return y;
                }
            }
            return y;
        }

        return 300;
    }

    public void CheckClickMoveTo(int x, int y)
    {
        IntPtr gs = GameScrPointer;
        if (gs != IntPtr.Zero)
        {
            _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodCheckClickMoveTo, gs, x, y, 0);
        }
    }

    public void DoDoubleClickToObj(IntPtr objPtr)
    {
        IntPtr gs = GameScrPointer;
        if (gs != IntPtr.Zero && objPtr != IntPtr.Zero)
        {
            _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodDoDoubleClickToObj, gs, objPtr);
        }
    }

    public bool ClickMoving
    {
        get
        {
            IntPtr gs = GameScrPointer;
            return gs != IntPtr.Zero && _reflector.ReadField<bool>(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldClickMoving, gs);
        }
        set
        {
            IntPtr gs = GameScrPointer;
            if (gs != IntPtr.Zero)
                _reflector.WriteField(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldClickMoving, gs, value);
        }
    }

    public bool ChangeZone(int zoneId)
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodRequestChangeZone, service, zoneId, -1);
            return true;
        }
        catch { return false; }
    }

    public void RequestZoneList()
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodOpenUIZone, service);
                SuppressZonePanel();
            }
        }
        catch { }
    }

    public void CloseZonePanel()
    {
        _ui.CloseZonePanel();
    }

    private void SuppressZonePanel()
    {
        _ = Task.Run(async () =>
        {
            try
            {
                for (int i = 0; i < 40; i++)
                {
                    if (_ui.IsZonePanelOpen)
                    {
                        _ui.CloseZonePanel();
                    }
                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
            catch { }
        });
    }

    public WorldSnapshot GetSnapshot()
    {
        if (!_ui.IsGamePlayScreen) return WorldSnapshot.Empty;

        try
        {
            var rawMobs = GetMonsters();
            var rawPlayers = GetPlayers();
            var rawNpcs = GetNpcs();
            var rawWaypoints = GetWaypoints();
            var rawZones = GetZones();

            var mobs = rawMobs.Select(m => new WorldMobInfo(
                m.Id, m.TemplateId, m.Name, m.Hp, m.MaxHp, m.X, m.Y, m.Status, m.IsAlive, m.IsBoss, m.IsSuperMob, m.IsFlying
            )).ToList();

            var bosses = rawPlayers.Where(p => p.IsBoss).Select(b => new WorldBossInfo(
                b.Id, b.Name, b.Hp, b.MaxHp, b.X, b.Y
            )).ToList();

            var players = rawPlayers.Where(p => !p.IsBoss).Select(p => new WorldPlayerInfo(
                p.Id, p.Name, p.Hp, p.MaxHp, p.X, p.Y
            )).ToList();

            var npcs = rawNpcs.Select(n => new WorldNpcInfo(
                n.Id, n.Name, n.X, n.Y
            )).ToList();

            var waypoints = rawWaypoints.Select(w => new WorldWaypointInfo(
                w.Name, w.X, w.Y, w.MinX, w.MinY, w.MaxX, w.MaxY
            )).ToList();

            int aliveCount = mobs.Count(m => m.IsAlive);
            var currentZone = rawZones.FirstOrDefault(z => z.ZoneId == ZoneId);
            int maxPlayers = currentZone?.MaxPlayers ?? (rawZones.FirstOrDefault(z => z.MaxPlayers > 0)?.MaxPlayers ?? 15);
            int currentPlayers = currentZone != null && currentZone.NumPlayers > 0
                ? currentZone.NumPlayers
                : (players.Count + bosses.Count + 1);

            return new WorldSnapshot(
                MapId: MapId,
                ZoneId: ZoneId,
                MapName: MapName,
                TotalMobs: mobs.Count,
                AliveMobs: aliveCount,
                CurrentPlayers: currentPlayers,
                MaxPlayers: maxPlayers,
                Mobs: mobs,
                Bosses: bosses,
                Players: players,
                Waypoints: waypoints,
                Npcs: npcs,
                Zones: rawZones
            );
        }
        catch
        {
            return WorldSnapshot.Empty;
        }
    }

    private (string Name, bool IsFlying) GetMobTemplateInfo(int templateId)
    {
        if (templateId < 0) return (string.Empty, false);
        if (_mobTemplateCache.TryGetValue(templateId, out var cachedInfo))
            return cachedInfo;

        try
        {
            IntPtr arrMobTemplatePtr = _reflector.ReadStatic<IntPtr>(GameSchema.Mob.ClassName, GameSchema.Mob.StaticArrMobTemplate);
            if (arrMobTemplatePtr == IntPtr.Zero) return (string.Empty, false);

            IntPtr[] templates = _reflector.ReadMonoPointerArray(arrMobTemplatePtr);
            if (templateId < templates.Length && templates[templateId] != IntPtr.Zero)
            {
                string name = _reflector.ReadStringField(GameSchema.MobTemplate.ClassName, GameSchema.MobTemplate.FieldName, templates[templateId]);
                sbyte type = _reflector.ReadField<sbyte>(GameSchema.MobTemplate.ClassName, GameSchema.MobTemplate.FieldType, templates[templateId]);
                bool isFlying = type == 4 || type == 5;
                var info = (name ?? string.Empty, isFlying);
                _mobTemplateCache[templateId] = info;
                return info;
            }
        }
        catch { }

        return (string.Empty, false);
    }

    private string GetNpcTemplateName(int templateId)
    {
        if (templateId < 0) return string.Empty;
        if (_npcTemplateCache.TryGetValue(templateId, out var cachedName))
            return cachedName;

        try
        {
            IntPtr arrNpcTemplatePtr = _reflector.ReadStatic<IntPtr>(GameSchema.Npc.ClassName, GameSchema.Npc.StaticArrNpcTemplate);
            if (arrNpcTemplatePtr == IntPtr.Zero) return string.Empty;

            IntPtr[] templates = _reflector.ReadMonoPointerArray(arrNpcTemplatePtr);
            if (templateId < templates.Length && templates[templateId] != IntPtr.Zero)
            {
                string name = _reflector.ReadStringField(GameSchema.NpcTemplate.ClassName, GameSchema.NpcTemplate.FieldName, templates[templateId]);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _npcTemplateCache[templateId] = name;
                    return name;
                }
            }
        }
        catch { }

        return string.Empty;
    }
}
