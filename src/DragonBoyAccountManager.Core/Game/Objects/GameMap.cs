using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Game.Objects;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameMap(MonoReflector reflector) : GameObject(reflector)
{
    public override string ClassName => "TileMap";

    private readonly ConcurrentDictionary<int, string> _mobTemplateCache = new();
    private readonly ConcurrentDictionary<int, string> _npcTemplateCache = new();
    private int _cachedCollisionMapId = -1;
    private (int Width, int Height, int[] Types)? _cachedCollisionData;

    public (int Width, int Height, int[] Types)? GetTileMapCollisionData()
    {
        try
        {
            int tmw = Reflector.ReadStatic<int>("TileMap", "tmw");
            int tmh = Reflector.ReadStatic<int>("TileMap", "tmh");
            if (tmw <= 0 || tmh <= 0) return null;

            int mapId = MapId;
            if (mapId >= 0 && _cachedCollisionMapId == mapId && _cachedCollisionData != null)
                return _cachedCollisionData;

            IntPtr typesPtr = Reflector.ReadStatic<IntPtr>("TileMap", "types");
            if (typesPtr == IntPtr.Zero)
            {
                typesPtr = Reflector.ReadStatic<IntPtr>("TileMap", "maps");
            }

            if (typesPtr == IntPtr.Zero) return null;

            int[] types = Reflector.ReadMonoIntArray(typesPtr, 300_000);
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

    public int MapId
    {
        get
        {
            try
            {
                return ReadStatic<int>("mapID");
            }
            catch
            {
                return -1;
            }
        }
    }

    public int ZoneId
    {
        get
        {
            try
            {
                return ReadStatic<int>("zoneID");
            }
            catch
            {
                return -1;
            }
        }
    }

    public string MapName
    {
        get
        {
            try
            {
                string name = ReadStaticString("mapName");
                return string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public bool ChangeZone(int zoneId)
    {
        try
        {
            IntPtr curScreen = Reflector.ReadStatic<IntPtr>("GameCanvas", "currentScreen");
            if (curScreen == IntPtr.Zero || Reflector.GetClassName(curScreen) != "GameScr")
                return false;

            IntPtr service = Reflector.CallMethod("Service", "gI", IntPtr.Zero);
            if (service == IntPtr.Zero)
            {
                service = Reflector.ReadStatic<IntPtr>("Service", "instance");
            }

            if (service != IntPtr.Zero)
            {
                Reflector.CallMethod("Service", "requestChangeZone", service, zoneId, -1);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void RequestZoneList()
    {
        try
        {
            IntPtr curScreen = Reflector.ReadStatic<IntPtr>("GameCanvas", "currentScreen");
            if (curScreen == IntPtr.Zero || Reflector.GetClassName(curScreen) != "GameScr")
                return;

            IntPtr service = Reflector.CallMethod("Service", "gI", IntPtr.Zero);
            if (service == IntPtr.Zero)
            {
                service = Reflector.ReadStatic<IntPtr>("Service", "instance");
            }

            if (service != IntPtr.Zero)
            {
                Reflector.CallMethod("Service", "openUIZone", service);
                Task.Delay(150).ContinueWith(_ =>
                {
                    try
                    {
                        IntPtr panel = Reflector.ReadStatic<IntPtr>("GameCanvas", "panel");
                        if (panel != IntPtr.Zero)
                        {
                            Reflector.WriteField("Panel", "isShow", panel, false);
                        }
                    }
                    catch { }
                });
            }
        }
        catch { }
    }

    public static int GetWaypointX(short minX, short maxX, int pxw)
    {
        return maxX < 60 ? 15 :
               minX > pxw - 60 ? pxw - 15 :
               minX + ((maxX - minX) / 2);
    }

    public static int GetWaypointY(short maxY)
    {
        return maxY;
    }

    public bool MoveToWaypoint(int targetX, int targetY)
    {
        try
        {
            IntPtr curScreen = Reflector.ReadStatic<IntPtr>("GameCanvas", "currentScreen");
            if (curScreen == IntPtr.Zero || Reflector.GetClassName(curScreen) != "GameScr")
                return false;

            IntPtr vGoPtr = Reflector.ReadStatic<IntPtr>("TileMap", "vGo");
            if (vGoPtr == IntPtr.Zero) return false;
            var wpPointers = Reflector.ReadMyVector(vGoPtr);
            if (wpPointers.Count == 0) return false;

            int pxw = Reflector.ReadStatic<int>("TileMap", "pxw");
            if (pxw <= 0)
            {
                int tmw = Reflector.ReadStatic<int>("TileMap", "tmw");
                pxw = (tmw > 0 ? tmw : 100) * 24;
            }

            IntPtr bestWp = IntPtr.Zero;
            int minDistance = int.MaxValue;
            short bestMinX = 0, bestMinY = 0, bestMaxX = 0, bestMaxY = 0;
            bool bestIsEnter = false, bestIsOffline = false;

            foreach (var wpPtr in wpPointers)
            {
                short minX = Reflector.ReadField<short>("Waypoint", "minX", wpPtr);
                short minY = Reflector.ReadField<short>("Waypoint", "minY", wpPtr);
                short maxX = Reflector.ReadField<short>("Waypoint", "maxX", wpPtr);
                short maxY = Reflector.ReadField<short>("Waypoint", "maxY", wpPtr);
                bool isEnter = Reflector.ReadField<bool>("Waypoint", "isEnter", wpPtr);
                bool isOffline = Reflector.ReadField<bool>("Waypoint", "isOffline", wpPtr);

                int x = GetWaypointX(minX, maxX, pxw);
                int y = GetWaypointY(maxY);

                int dist = Math.Abs(x - targetX) + Math.Abs(y - targetY);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestWp = wpPtr;
                    bestMinX = minX;
                    bestMinY = minY;
                    bestMaxX = maxX;
                    bestMaxY = maxY;
                    bestIsEnter = isEnter;
                    bestIsOffline = isOffline;
                }
            }

            if (bestWp != IntPtr.Zero)
            {
                Reflector.CallMethod("Waypoint", "perform", bestWp, 2, null);
                return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }

    public bool FocusOrClickObject(string entityType, int entityId)
    {
        try
        {
            IntPtr curScreen = Reflector.ReadStatic<IntPtr>("GameCanvas", "currentScreen");
            if (curScreen == IntPtr.Zero || Reflector.GetClassName(curScreen) != "GameScr")
                return false;

            IntPtr myCharPtr = Reflector.ReadStatic<IntPtr>("Char", "myChar");
            if (myCharPtr == IntPtr.Zero)
                return false;

            IntPtr targetPtr = IntPtr.Zero;
            string actualType = entityType.ToLowerInvariant();

            if (actualType == "mob")
            {
                IntPtr vMobPtr = Reflector.ReadStatic<IntPtr>("GameScr", "vMob");
                if (vMobPtr != IntPtr.Zero)
                {
                    var mobPointers = Reflector.ReadMyVector(vMobPtr);
                    foreach (var ptr in mobPointers)
                    {
                        int id = Reflector.ReadField<int>("Mob", "mobId", ptr);
                        if (id == entityId)
                        {
                            targetPtr = ptr;
                            break;
                        }
                    }
                }
            }
            else if (actualType == "npc")
            {
                IntPtr vNpcPtr = Reflector.ReadStatic<IntPtr>("GameScr", "vNpc");
                if (vNpcPtr != IntPtr.Zero)
                {
                    var npcPointers = Reflector.ReadMyVector(vNpcPtr);
                    foreach (var ptr in npcPointers)
                    {
                        int id = Reflector.ReadField<int>("Npc", "npcId", ptr);
                        if (id == entityId)
                        {
                            targetPtr = ptr;
                            break;
                        }
                    }
                }
            }
            else if (actualType == "boss")
            {
                // Boss could be a Char in vCharInMap or a Boss Mob in vMob
                IntPtr vCharPtr = Reflector.ReadStatic<IntPtr>("GameScr", "vCharInMap");
                if (vCharPtr != IntPtr.Zero)
                {
                    var charPointers = Reflector.ReadMyVector(vCharPtr);
                    foreach (var ptr in charPointers)
                    {
                        int id = Reflector.ReadField<int>("Char", "charID", ptr);
                        if (id == entityId)
                        {
                            targetPtr = ptr;
                            actualType = "char";
                            break;
                        }
                    }
                }

                if (targetPtr == IntPtr.Zero)
                {
                    IntPtr vMobPtr = Reflector.ReadStatic<IntPtr>("GameScr", "vMob");
                    if (vMobPtr != IntPtr.Zero)
                    {
                        var mobPointers = Reflector.ReadMyVector(vMobPtr);
                        foreach (var ptr in mobPointers)
                        {
                            int id = Reflector.ReadField<int>("Mob", "mobId", ptr);
                            if (id == entityId)
                            {
                                targetPtr = ptr;
                                actualType = "mob";
                                break;
                            }
                        }
                    }
                }
            }
            else // player, char
            {
                IntPtr vCharPtr = Reflector.ReadStatic<IntPtr>("GameScr", "vCharInMap");
                if (vCharPtr != IntPtr.Zero)
                {
                    var charPointers = Reflector.ReadMyVector(vCharPtr);
                    foreach (var ptr in charPointers)
                    {
                        int id = Reflector.ReadField<int>("Char", "charID", ptr);
                        if (id == entityId)
                        {
                            targetPtr = ptr;
                            actualType = "char";
                            break;
                        }
                    }
                }
            }

            if (targetPtr == IntPtr.Zero)
                return false;

            // 1. Direct field writes to ensure focus is set immediately
            if (actualType == "mob")
            {
                Reflector.WriteField("Char", "mobFocus", myCharPtr, targetPtr);
                Reflector.WriteField("Char", "charFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField("Char", "npcFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField("Char", "itemFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField<bool>("Char", "isManualFocus", myCharPtr, true);
            }
            else if (actualType == "npc")
            {
                Reflector.WriteField("Char", "npcFocus", myCharPtr, targetPtr);
                Reflector.WriteField("Char", "mobFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField("Char", "charFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField("Char", "itemFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField<bool>("Char", "isManualFocus", myCharPtr, true);
            }
            else // char
            {
                Reflector.WriteField("Char", "charFocus", myCharPtr, targetPtr);
                Reflector.WriteField("Char", "mobFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField("Char", "npcFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField("Char", "itemFocus", myCharPtr, IntPtr.Zero);
                Reflector.WriteField<bool>("Char", "isManualFocus", myCharPtr, true);
            }

            // 2. Call focusManualTo
            try
            {
                Reflector.CallMethod("Char", "focusManualTo", myCharPtr, targetPtr);
            }
            catch { }

            // 3. Call doDoubleClickToObj on GameScr
            try
            {
                IntPtr gameScr = Reflector.CallMethod("GameScr", "gI", IntPtr.Zero);
                if (gameScr == IntPtr.Zero) gameScr = Reflector.ReadStatic<IntPtr>("GameScr", "instance");
                if (gameScr != IntPtr.Zero)
                {
                    Reflector.CallMethod("GameScr", "doDoubleClickToObj", gameScr, targetPtr);
                }
            }
            catch { }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public string GetMobTemplateName(int templateId)
    {
        if (_mobTemplateCache.TryGetValue(templateId, out var cached))
            return cached;

        try
        {
            IntPtr arrPtr = Reflector.ReadStatic<IntPtr>("Mob", "arrMobTemplate");
            if (arrPtr != IntPtr.Zero)
            {
                int ptrSize = Reflector.Context.Memory.PointerSize;
                int length = Reflector.Context.Memory.ReadInt(arrPtr + ptrSize * 3);
                if (templateId >= 0 && templateId < length)
                {
                    IntPtr templatePtr = Reflector.Context.Memory.ReadIntPtr(arrPtr + ptrSize * 4 + templateId * ptrSize);
                    if (templatePtr != IntPtr.Zero)
                    {
                        string name = Reflector.ReadStringField("MobTemplate", "name", templatePtr);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            string cleanName = name.Trim();
                            _mobTemplateCache[templateId] = cleanName;
                            return cleanName;
                        }
                    }
                }
            }
        }
        catch { }

        string fallback = $"Quái #{templateId}";
        _mobTemplateCache[templateId] = fallback;
        return fallback;
    }

    public string GetNpcTemplateName(int templateId)
    {
        if (_npcTemplateCache.TryGetValue(templateId, out var cached))
            return cached;

        try
        {
            IntPtr arrPtr = Reflector.ReadStatic<IntPtr>("Npc", "arrNpcTemplate");
            if (arrPtr != IntPtr.Zero)
            {
                int ptrSize = Reflector.Context.Memory.PointerSize;
                int length = Reflector.Context.Memory.ReadInt(arrPtr + ptrSize * 3);
                if (templateId >= 0 && templateId < length)
                {
                    IntPtr templatePtr = Reflector.Context.Memory.ReadIntPtr(arrPtr + ptrSize * 4 + templateId * ptrSize);
                    if (templatePtr != IntPtr.Zero)
                    {
                        string name = Reflector.ReadStringField("NpcTemplate", "name", templatePtr);
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            string cleanName = name.Trim();
                            _npcTemplateCache[templateId] = cleanName;
                            return cleanName;
                        }
                    }
                }
            }
        }
        catch { }

        return string.Empty;
    }

    public MapSnapshot GetMapSnapshot()
    {
        try
        {
            // BẢO VỆ TUYỆT ĐỐI: Chỉ đọc dữ liệu GameScr khi nhân vật đã thực sự vào màn hình GameScr
            // Nếu truy cập GameScr từ remote thread trước khi game tự khởi tạo trên Main thread,
            // Mono sẽ chạy GameScr..cctor() -> Info..cctor() -> Texture2D.Internal_Create -> CRASH 0xc0000005!
            IntPtr curScreen = Reflector.ReadStatic<IntPtr>("GameCanvas", "currentScreen");
            if (curScreen == IntPtr.Zero || Reflector.GetClassName(curScreen) != "GameScr")
            {
                return MapSnapshot.Empty;
            }

            IntPtr myChar = Reflector.ReadStatic<IntPtr>("Char", "myChar");
            if (myChar == IntPtr.Zero)
            {
                return MapSnapshot.Empty;
            }

            int mapId = MapId;
            int zoneId = ZoneId;
            string mapName = MapName;

            if (mapId < 0)
            {
                return MapSnapshot.Empty;
            }

            // 1. Waypoints
            var waypoints = new List<MapWaypointInfo>();
            IntPtr vGoPtr = Reflector.ReadStatic<IntPtr>("TileMap", "vGo");
            if (vGoPtr != IntPtr.Zero)
            {
                int pxw = Reflector.ReadStatic<int>("TileMap", "pxw");
                if (pxw <= 0)
                {
                    int tmw = Reflector.ReadStatic<int>("TileMap", "tmw");
                    pxw = (tmw > 0 ? tmw : 100) * 24;
                }

                var wpPointers = Reflector.ReadMyVector(vGoPtr);
                foreach (var wpPtr in wpPointers)
                {
                    short minX = Reflector.ReadField<short>("Waypoint", "minX", wpPtr);
                    short minY = Reflector.ReadField<short>("Waypoint", "minY", wpPtr);
                    short maxX = Reflector.ReadField<short>("Waypoint", "maxX", wpPtr);
                    short maxY = Reflector.ReadField<short>("Waypoint", "maxY", wpPtr);

                    string name = string.Empty;
                    IntPtr popupPtr = Reflector.ReadFieldPtr("Waypoint", "popup", wpPtr);
                    if (popupPtr != IntPtr.Zero)
                    {
                        IntPtr saysArrayPtr = Reflector.ReadFieldPtr("PopUp", "says", popupPtr);
                        if (saysArrayPtr != IntPtr.Zero)
                        {
                            string[] says = Reflector.ReadMonoStringArray(saysArrayPtr);
                            if (says.Length > 0)
                            {
                                name = string.Join(" ", says).Trim();
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = "Cổng sang map";
                    }

                    int x = GetWaypointX(minX, maxX, pxw);
                    int y = GetWaypointY(maxY);

                    waypoints.Add(new MapWaypointInfo(name, x, y, minX, minY, maxX, maxY));
                }
            }

            // 2. NPCs
            var npcs = new List<MapNpcInfo>();
            IntPtr vNpcPtr = Reflector.ReadStatic<IntPtr>("GameScr", "vNpc");
            if (vNpcPtr != IntPtr.Zero)
            {
                var npcPointers = Reflector.ReadMyVector(vNpcPtr);
                foreach (var npcPtr in npcPointers)
                {
                    int npcId = Reflector.ReadField<int>("Npc", "npcId", npcPtr);
                    int cx = Reflector.ReadField<int>("Npc", "cx", npcPtr);
                    int cy = Reflector.ReadField<int>("Npc", "cy", npcPtr);

                    IntPtr templatePtr = Reflector.ReadFieldPtr("Npc", "template", npcPtr);
                    string name = templatePtr != IntPtr.Zero ? Reflector.ReadStringField("NpcTemplate", "name", templatePtr) : string.Empty;

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = Reflector.ReadStringField("Char", "cName", npcPtr);
                    }
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = GetNpcTemplateName(npcId);
                    }
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = $"NPC #{npcId}";
                    }

                    npcs.Add(new MapNpcInfo(npcId, name.Trim(), cx, cy));
                }
            }

            // 3. Mobs
            var mobs = new List<MapMobInfo>();
            var bosses = new List<MapBossInfo>();
            int aliveMobs = 0;

            IntPtr vMobPtr = Reflector.ReadStatic<IntPtr>("GameScr", "vMob");
            if (vMobPtr != IntPtr.Zero)
            {
                var mobPointers = Reflector.ReadMyVector(vMobPtr);
                foreach (var mobPtr in mobPointers)
                {
                    int mobId = Reflector.ReadField<int>("Mob", "mobId", mobPtr);
                    int templateId = Reflector.ReadField<int>("Mob", "templateId", mobPtr);
                    long hp = Reflector.ReadField<long>("Mob", "hp", mobPtr);
                    long maxHp = Reflector.ReadField<long>("Mob", "maxHp", mobPtr);
                    int status = Reflector.ReadField<int>("Mob", "status", mobPtr);
                    int x = Reflector.ReadField<int>("Mob", "x", mobPtr);
                    int y = Reflector.ReadField<int>("Mob", "y", mobPtr);
                    bool isBoss = Reflector.ReadField<bool>("Mob", "isBoss", mobPtr);
                    sbyte levelBoss = Reflector.ReadField<sbyte>("Mob", "levelBoss", mobPtr);

                    bool isAlive = status != 0 && status != 1 && hp > 0;
                    if (isAlive) aliveMobs++;

                    string mobName = GetMobTemplateName(templateId);
                    if (string.IsNullOrWhiteSpace(mobName))
                    {
                        mobName = $"Quái #{templateId}";
                    }
                    bool isBossMob = isBoss || levelBoss > 0;

                    mobs.Add(new MapMobInfo(mobId, templateId, mobName, hp, maxHp, x, y, status, isAlive, isBossMob));

                    if (isBossMob && isAlive)
                    {
                        bosses.Add(new MapBossInfo(mobId, mobName, hp, maxHp, x, y));
                    }
                }
            }

            // 4. Players & Bosses & Đệ tử (from GameScr.vCharInMap)
            var players = new List<MapPlayerInfo>();
            IntPtr vCharPtr = Reflector.ReadStatic<IntPtr>("GameScr", "vCharInMap");
            if (vCharPtr != IntPtr.Zero)
            {
                var charPointers = Reflector.ReadMyVector(vCharPtr);
                foreach (var charPtr in charPointers)
                {
                    int charId = Reflector.ReadField<int>("Char", "charID", charPtr);
                    string name = Reflector.ReadStringField("Char", "cName", charPtr);
                    int cx = Reflector.ReadField<int>("Char", "cx", charPtr);
                    int cy = Reflector.ReadField<int>("Char", "cy", charPtr);
                    long cHP = Reflector.ReadField<long>("Char", "cHP", charPtr);
                    long cHPFull = Reflector.ReadField<long>("Char", "cHPFull", charPtr);
                    sbyte cTypePk = Reflector.ReadField<sbyte>("Char", "cTypePk", charPtr);
                    bool isPet = Reflector.ReadField<bool>("Char", "isPet", charPtr);
                    bool isMiniPet = Reflector.ReadField<bool>("Char", "isMiniPet", charPtr);

                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    string rawName = name.Trim();
                    bool isPetChar = isPet || isMiniPet || rawName.StartsWith("$") || rawName.StartsWith("#") || rawName.Contains("Đệ tử");
                    string cleanName = rawName.StartsWith("$") || rawName.StartsWith("#") ? rawName.Substring(1).Trim() : rawName;

                    if (isPetChar)
                    {
                        // Đệ tử (Pet / Disciple) - không phải Boss!
                        players.Add(new MapPlayerInfo(charId, $"[Đệ] {cleanName}", cHP, cHPFull, cx, cy));
                    }
                    else if (cTypePk == 5 || (charId < 0 && cTypePk != 0))
                    {
                        // Boss thật sự (cTypePk == 5)
                        bosses.Add(new MapBossInfo(charId, cleanName, cHP, cHPFull, cx, cy));
                    }
                    else
                    {
                        // Người chơi khác trong map
                        players.Add(new MapPlayerInfo(charId, cleanName, cHP, cHPFull, cx, cy));
                    }
                }
            }

            // 5. Zone player counts (tổng người / max người trong khu) & Danh sách khu theo code gốc openzonemap/Panel
            int currentPlayers = players.Count + 1; // Số người chơi khác + bản thân
            int maxPlayers = 15;
            var mapZones = new List<MapZoneInfo>();

            try
            {
                IntPtr gameScr = Reflector.CallMethod("GameScr", "gI", IntPtr.Zero);
                if (gameScr == IntPtr.Zero) gameScr = Reflector.ReadStatic<IntPtr>("GameScr", "instance");
                if (gameScr != IntPtr.Zero)
                {
                    IntPtr zonesPtr = Reflector.ReadFieldPtr("GameScr", "zones", gameScr);
                    IntPtr maxPlayerPtr = Reflector.ReadFieldPtr("GameScr", "maxPlayer", gameScr);
                    IntPtr numPlayerPtr = Reflector.ReadFieldPtr("GameScr", "numPlayer", gameScr);
                    IntPtr ptsPtr = Reflector.ReadFieldPtr("GameScr", "pts", gameScr);

                    if (zonesPtr != IntPtr.Zero)
                    {
                        var zonesArr = Reflector.ReadMonoIntArray(zonesPtr);
                        var maxArr = maxPlayerPtr != IntPtr.Zero ? Reflector.ReadMonoIntArray(maxPlayerPtr) : [];
                        var numArr = numPlayerPtr != IntPtr.Zero ? Reflector.ReadMonoIntArray(numPlayerPtr) : [];
                        var ptsArr = ptsPtr != IntPtr.Zero ? Reflector.ReadMonoIntArray(ptsPtr) : [];

                        for (int i = 0; i < zonesArr.Length; i++)
                        {
                            int zId = zonesArr[i];
                            if (zId < 0) continue;
                            int mP = (i < maxArr.Length && maxArr[i] > 0) ? maxArr[i] : 15;
                            int nP = (i < numArr.Length && numArr[i] >= 0) ? numArr[i] : 0;
                            int st = (i < ptsArr.Length) ? ptsArr[i] : 0;

                            mapZones.Add(new MapZoneInfo(zId, nP, mP, st));

                            if (zId == zoneId)
                            {
                                maxPlayers = mP;
                                if (nP > 0)
                                {
                                    currentPlayers = nP;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // Fallback nếu game client chưa gọi openUIZone (hoặc zones chưa khởi tạo)
            if (mapZones.Count == 0)
            {
                for (int i = 0; i <= 19; i++)
                {
                    mapZones.Add(new MapZoneInfo(i, i == zoneId ? currentPlayers : 0, 15, 0));
                }
            }

            return new MapSnapshot(
                MapId: mapId,
                ZoneId: zoneId,
                MapName: mapName,
                TotalMobs: mobs.Count,
                AliveMobs: aliveMobs,
                CurrentPlayers: currentPlayers,
                MaxPlayers: maxPlayers,
                Mobs: mobs,
                Bosses: bosses,
                Players: players,
                Waypoints: waypoints,
                Npcs: npcs,
                Zones: mapZones
            );
        }
        catch
        {
            return MapSnapshot.Empty;
        }
    }
}
