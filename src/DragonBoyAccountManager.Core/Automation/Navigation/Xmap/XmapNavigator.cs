using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public static class XmapNavigator
{
    private const int MaxTransitions = 40;
    private const int MaxRetriesPerHop = 3;

    public static async Task<Result> NavigateToMapAsync(
        ISessionContext session,
        int targetMapId,
        bool isFastTeleport = false,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (session == null || !session.IsRunning || !session.CurrentPlayer.IsInGame)
            return Result.Failure("Nhân vật chưa vào game.");

        if (targetMapId < 0)
            return Result.Failure("Map đích không hợp lệ.");

        var graph = WorldMapGraph.Instance;

        session.Tick();
        if (session.CurrentWorld.MapId == targetMapId)
        {
            return Result.Success($"Đã ở map đích #{targetMapId} ({WorldMapGraph.GetMapName(targetMapId)}).");
        }

        if (session.CurrentPlayer.IsChangingMap || session.CurrentMap.Width <= 0)
        {
            int initWaitMs = 0;
            while (initWaitMs < 2000 && (session.CurrentPlayer.IsChangingMap || session.CurrentMap.Width <= 0))
            {
                await Task.Delay(50, cancellationToken);
                initWaitMs += 50;
                session.Tick();
            }
        }

        int transitionsCount = 0;

        while (transitionsCount < MaxTransitions)
        {
            if (cancellationToken.IsCancellationRequested)
                return Result.Failure("Quá trình Xmap bị hủy.");

            session.Tick();
            int currentMapId = session.CurrentWorld.MapId;
            if (currentMapId == targetMapId)
            {
                return Result.Success($"Đã tới {session.CurrentWorld.MapName} (Map #{targetMapId})");
            }

            if (currentMapId < 0)
            {
                await Task.Delay(300, cancellationToken);
                continue;
            }

            var path = graph.FindPath(
                currentMapId,
                targetMapId,
                session.CurrentPlayer.Gender,
                -1,
                session.CurrentPlayer.Power,
                true);

            if (path == null || path.Count == 0)
            {
                return Result.Failure($"Không tìm thấy đường từ {WorldMapGraph.GetMapName(currentMapId)} (Map #{currentMapId}) đến {WorldMapGraph.GetMapName(targetMapId)} (Map #{targetMapId}).");
            }

            int nextMapId = path[0];
            string nextMapName = WorldMapGraph.GetMapName(nextMapId);
            bool isNpcHop = WorldMapGraph.IsNpcTransitionEdge(currentMapId, nextMapId) ||
                            WorldMapGraph.NpcActions.ContainsKey((currentMapId, nextMapId));

            int startMapId = currentMapId;
            int hopRetries = 0;
            bool hopSuccess = false;

            while (hopRetries < MaxRetriesPerHop)
            {
                if (cancellationToken.IsCancellationRequested)
                    return Result.Failure("Quá trình Xmap bị hủy.");

                session.Tick();
                if (session.CurrentWorld.MapId == targetMapId)
                {
                    return Result.Success($"Đã tới {session.CurrentWorld.MapName} (Map #{targetMapId})");
                }

                if (session.CurrentWorld.MapId != startMapId && session.CurrentWorld.MapId >= 0)
                {
                    hopSuccess = true;
                    break;
                }

                string hopTypeName = isNpcHop ? "Hành tinh/NPC" : "Waypoint";
                if (hopRetries > 0)
                {
                    progress?.Report($"[Xmap] Chuyển map chậm ({hopTypeName}), thử lại lần {hopRetries + 1}/{MaxRetriesPerHop} chặng {session.CurrentWorld.MapName} -> {nextMapName}...");
                    await Task.Delay(250, cancellationToken);
                }
                else
                {
                    progress?.Report($"[Xmap] Chặng {transitionsCount + 1}: {session.CurrentWorld.MapName} -> {nextMapName} ({hopTypeName}, còn {path.Count} chặng)...");
                }

                var hopExecResult = await ExecuteHopAsync(session, currentMapId, nextMapId, isNpcHop, isFastTeleport, cancellationToken);
                if (hopExecResult.IsFailure)
                {
                    if (hopRetries >= MaxRetriesPerHop - 1)
                    {
                        return hopExecResult;
                    }

                    hopRetries++;
                    await Task.Delay(250, cancellationToken);
                    continue;
                }

                bool mapChanged = await WaitForMapSettledAsync(session, startMapId, isNpcHop, cancellationToken);
                if (mapChanged)
                {
                    hopSuccess = true;
                    break;
                }

                hopRetries++;
            }

            if (!hopSuccess)
            {
                return Result.Failure($"Không thể chuyển từ Map #{startMapId} sang {nextMapName} sau {MaxRetriesPerHop} lần thử.");
            }

            transitionsCount++;
            await Task.Delay(50, cancellationToken);
        }

        session.Tick();
        if (session.CurrentWorld.MapId == targetMapId)
        {
            return Result.Success($"Đã tới {session.CurrentWorld.MapName} (Map #{targetMapId})");
        }

        return Result.Failure($"Vượt quá số lần chuyển map tối đa ({MaxTransitions}) mà chưa tới đích.");
    }

    public static int GetMapPixelWidth(IGameRuntime runtime, int fallbackWidth = 0)
    {
        int pxw = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.Pxw);
        if (pxw > 0) return pxw;
        int tmw = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.Tmw);
        if (tmw > 0) return tmw * MapSnapshot.TileSize;
        return fallbackWidth > 0 ? fallbackWidth : 2400;
    }

    public static (int X, int Y) ResolveWaypointTarget(WorldWaypointInfo wp, int mapPixelWidth, MapSnapshot? map = null)
    {
        int x = GetWaypointTargetX(wp, mapPixelWidth);
        int y = wp.MaxY > 0 ? wp.MaxY : wp.Y;
        if (map is { IsValid: true })
        {
            return map.ResolveWaypointPosition(wp.MinX, wp.MinY, wp.MaxX, wp.MaxY, x, y);
        }
        return (x, y);
    }

    public static async Task TeleportToAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int targetX,
        int targetY,
        CancellationToken cancellationToken = default,
        MapSnapshot? map = null)
    {
        var charLayout = runtime.Layout.Char;
        int curStatus = runtime.ReadField<int>(charPtr, charLayout.StatusMe);
        int curX = runtime.ReadField<int>(charPtr, charLayout.X);
        int curY = runtime.ReadField<int>(charPtr, charLayout.Y);

        if (curStatus == 16 && charLayout.CxSend >= 0 && charLayout.CySend >= 0)
        {
            int sentX = runtime.ReadField<int>(charPtr, charLayout.CxSend);
            int sentY = runtime.ReadField<int>(charPtr, charLayout.CySend);
            if (sentX > 0 && sentY > 0)
            {
                curX = sentX;
                curY = sentY;
            }
        }

        int safeY = Math.Max(MapSnapshot.TileSize, targetY);
        const int maxStepX = 84;
        int distX = Math.Abs(targetX - curX);
        int steps = Math.Clamp((distX + maxStepX - 1) / maxStepX, 1, 30);

        for (int i = 1; i < steps; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            int stepX = curX + (targetX - curX) * i / steps;
            int lerpedY = curY + (safeY - curY) * i / steps;
            int stepY = lerpedY;

            if (map is { IsValid: true })
            {
                int groundY = map.GetYGround(stepX, lerpedY);
                if (groundY > 0)
                {
                    stepY = groundY;
                }
            }

            ResetMoveState(runtime, charPtr, 1);
            SendCharMovePacket(runtime, charPtr, servicePtr, stepX, stepY);
            await Task.Delay(12, cancellationToken);
        }

        ResetMoveState(runtime, charPtr, 1);
        SendCharMovePacket(runtime, charPtr, servicePtr, targetX, safeY);
        await Task.Yield();
    }

    public static async Task<Result> StepToWaypointAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        WorldWaypointInfo wp,
        int mapPixelWidth,
        CancellationToken cancellationToken = default,
        MapSnapshot? map = null)
    {
        int actualPxw = GetMapPixelWidth(runtime, mapPixelWidth);
        var (wpTargetX, wpTargetY) = ResolveWaypointTarget(wp, actualPxw, map);

        await ExecuteTeleportAndChangeMapAsync(
            runtime,
            charPtr,
            servicePtr,
            wpTargetX,
            wpTargetY,
            wp.IsOffline,
            cancellationToken,
            map);

        return Result.Success();
    }

    public static async Task ExecuteTeleportAndChangeMapAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int targetX,
        int targetY,
        bool isOffline,
        CancellationToken cancellationToken = default,
        MapSnapshot? map = null)
    {
        int startMapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);

        await TeleportToAsync(runtime, charPtr, servicePtr, targetX, targetY, cancellationToken, map);

        int nowMapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);
        bool isChanging = runtime.ReadStatic<bool>(GameSchema.Char.ClassName, runtime.Layout.Char.IsChangingMap) ||
                          runtime.ReadStatic<bool>(GameSchema.Char.ClassName, runtime.Layout.Char.IsLoadingMap) ||
                          runtime.ReadStatic<bool>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.IsLoading);

        if (nowMapId != startMapId || isChanging)
        {
            return;
        }

        string changeMapMethod = isOffline ? GameSchema.Service.MethodGetMapOffline : GameSchema.Service.MethodRequestChangeMap;
        runtime.CallMethod(GameSchema.Service.ClassName, changeMapMethod, servicePtr);
    }

    private static async Task<Result> ExecuteHopAsync(
        ISessionContext session,
        int currentMapId,
        int nextMapId,
        bool isNpcHop,
        bool isFastTeleport,
        CancellationToken cancellationToken)
    {
        var runtime = session.Runtime;
        IntPtr charPtr = runtime.GetMyCharPtr();
        IntPtr service = runtime.GetServicePtr();

        if (charPtr == IntPtr.Zero || service == IntPtr.Zero)
            return Result.Failure("Không tìm thấy con trỏ nhân vật hoặc Service.");

        int mapPixelWidth = GetMapPixelWidth(runtime, session.CurrentMap.Width * MapSnapshot.TileSize);
        int mapPixelHeight = GetMapPixelHeight(runtime, session.CurrentMap.Height * MapSnapshot.TileSize);

        if (isNpcHop)
        {
            var transitionNpc = FindTransitionNpc(currentMapId, nextMapId, session.CurrentWorld.Npcs);
            if (transitionNpc != null && transitionNpc.X > 0 && transitionNpc.Y > 0)
            {
                if (!isFastTeleport)
                {
                    var (npcX, npcY) = session.CurrentMap.IsValid
                        ? session.CurrentMap.ResolveSafePosition(transitionNpc.X, transitionNpc.Y, preferGround: true)
                        : (transitionNpc.X, transitionNpc.Y > MapSnapshot.TileSize ? (transitionNpc.Y / MapSnapshot.TileSize) * MapSnapshot.TileSize : transitionNpc.Y);

                    var navRes = await session.Navigator.NavigateAsync(
                        session.CurrentPlayer,
                        session.CurrentMap,
                        npcX,
                        npcY,
                        false,
                        null,
                        cancellationToken);
                    if (navRes.IsFailure)
                        return navRes;
                }

                await UseSpaceShipAsync(runtime, charPtr, service, transitionNpc, currentMapId, nextMapId, isFastTeleport, cancellationToken, session.CurrentMap);
                return Result.Success();
            }

            return Result.Failure($"Không tìm thấy NPC chuyển map từ #{currentMapId} sang #{nextMapId}.");
        }

        if (IsAirMapDownwardTransition(currentMapId, nextMapId))
        {
            return await StepToAirMapBottomAsync(
                runtime,
                charPtr,
                service,
                session.CurrentPlayer.X,
                mapPixelWidth,
                mapPixelHeight,
                cancellationToken,
                session.CurrentMap);
        }

        WorldWaypointInfo? matchedWp = null;
        int waitTimeMs = 0;
        const int maxWaitWpMs = 2500;
        const int pollIntervalMs = 60;
        int expectedWpCount = NextMapCatalog.GetExpectedWaypointCount(currentMapId, session.CurrentPlayer.Gender);

        while (waitTimeMs < maxWaitWpMs)
        {
            if (cancellationToken.IsCancellationRequested)
                return Result.Failure("Quá trình Xmap bị hủy.");

            session.Tick();

            if (!session.CurrentPlayer.IsChangingMap)
            {
                matchedWp = WorldMapGraph.FindWaypoint(
                    currentMapId,
                    nextMapId,
                    session.CurrentWorld.Waypoints,
                    expectedWpCount);

                if (matchedWp != null)
                {
                    break;
                }
            }

            await Task.Delay(pollIntervalMs, cancellationToken);
            waitTimeMs += pollIntervalMs;
        }

        if (matchedWp == null)
        {
            return Result.Failure($"Chưa tải được tọa độ cổng từ #{currentMapId} sang #{nextMapId} ({WorldMapGraph.GetMapName(nextMapId)}).");
        }

        int finalPixelWidth = GetMapPixelWidth(runtime, session.CurrentMap.Width * MapSnapshot.TileSize);

        if (!isFastTeleport)
        {
            var (wpTargetX, wpTargetY) = ResolveWaypointTarget(matchedWp, finalPixelWidth, session.CurrentMap);

            var navResult = await session.Navigator.NavigateAsync(
                session.CurrentPlayer,
                session.CurrentMap,
                wpTargetX,
                wpTargetY,
                false,
                null,
                cancellationToken);
            if (navResult.IsFailure)
                return navResult;

            await Task.Delay(80, cancellationToken);
            session.Tick();
            if (!session.CurrentPlayer.IsChangingMap)
            {
                string changeMapMethod = matchedWp.IsOffline ? GameSchema.Service.MethodGetMapOffline : GameSchema.Service.MethodRequestChangeMap;
                runtime.CallMethod(GameSchema.Service.ClassName, changeMapMethod, service);
            }
            return Result.Success();
        }

        return await StepToWaypointAsync(
            runtime,
            charPtr,
            service,
            matchedWp,
            finalPixelWidth,
            cancellationToken,
            session.CurrentMap);
    }

    private static async Task<bool> WaitForMapSettledAsync(
        ISessionContext session,
        int originalMapId,
        bool isNpcHop,
        CancellationToken cancellationToken)
    {
        int baseTimeoutMs = isNpcHop ? 8000 : 5000;
        int maxWaitTimeMs = baseTimeoutMs;
        int elapsedMs = 0;
        const int pollIntervalMs = 60;

        while (elapsedMs < maxWaitTimeMs)
        {
            if (cancellationToken.IsCancellationRequested)
                return false;

            await Task.Delay(pollIntervalMs, cancellationToken);
            elapsedMs += pollIntervalMs;

            session.Tick();
            int currentMapId = session.CurrentWorld.MapId;

            if (currentMapId != originalMapId && currentMapId >= 0)
            {
                int settleWaitMs = 0;
                const int maxSettleMs = 2500;
                int expectedWps = NextMapCatalog.GetExpectedWaypointCount(currentMapId, session.CurrentPlayer.Gender);

                while (settleWaitMs < maxSettleMs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    session.Tick();

                    bool isChanging = session.CurrentPlayer.IsChangingMap;
                    bool hasMapDimensions = session.CurrentMap.Width > 0 && session.CurrentMap.Height > 0;
                    bool hasPlayerPos = session.CurrentPlayer.X > 0 && session.CurrentPlayer.Y > 0;
                    bool hasWaypoints = expectedWps <= 0 ||
                                        (session.CurrentWorld.Waypoints.Count >= expectedWps &&
                                         session.CurrentWorld.Waypoints.All(w => !string.IsNullOrWhiteSpace(w.Name)));

                    if (!isChanging && hasMapDimensions && hasPlayerPos && hasWaypoints)
                    {
                        break;
                    }

                    await Task.Delay(50, cancellationToken);
                    settleWaitMs += 50;
                }

                await Task.Delay(350, cancellationToken);
                session.Tick();
                return session.CurrentWorld.MapId != originalMapId && session.CurrentWorld.MapId >= 0;
            }

            if (session.CurrentPlayer.IsChangingMap && maxWaitTimeMs < baseTimeoutMs + 6000)
            {
                maxWaitTimeMs = baseTimeoutMs + 6000;
            }
            else if (!isNpcHop && !session.CurrentPlayer.IsChangingMap && session.CurrentPlayer.StatusMe == 16)
            {
                return false;
            }
        }

        return false;
    }

    private static int GetMapPixelHeight(IGameRuntime runtime, int fallbackHeight = 0)
    {
        int pxh = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.Pxh);
        if (pxh > 0) return pxh;
        int tmh = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.Tmh);
        if (tmh > 0) return tmh * MapSnapshot.TileSize;
        return fallbackHeight > 0 ? fallbackHeight : 1000;
    }

    private static bool IsAirMapDownwardTransition(int currMapId, int nextMapId)
    {
        if (NextMapCatalog.TryGetMap(currMapId, out var map) && map != null)
        {
            var adj = map.Adjacents.FirstOrDefault(a => a.TargetMapId == nextMapId);
            if (adj != null)
            {
                return adj.TransitionType == NextMapTransitionType.AirMapDownward;
            }
        }
        return false;
    }

    private static void ResetMoveState(IGameRuntime runtime, IntPtr charPtr, int statusMe)
    {
        var charLayout = runtime.Layout.Char;
        runtime.WriteField(charPtr, charLayout.CurrentMovePoint, IntPtr.Zero);
        runtime.WriteField(charPtr, charLayout.Cvx, 0);
        runtime.WriteField(charPtr, charLayout.Cvy, 0);
        if (charLayout.Cp1 >= 0) runtime.WriteField(charPtr, charLayout.Cp1, 0);
        if (charLayout.Cp2 >= 0) runtime.WriteField(charPtr, charLayout.Cp2, 0);
        if (charLayout.Cp3 >= 0) runtime.WriteField(charPtr, charLayout.Cp3, 0);
        if (charLayout.IsTeleport >= 0) runtime.WriteField(charPtr, charLayout.IsTeleport, false);
        if (charLayout.TelePortSkill >= 0) runtime.WriteField(charPtr, charLayout.TelePortSkill, false);
        runtime.WriteField(charPtr, charLayout.StatusMe, statusMe);
    }

    private static void SendCharMovePacket(IGameRuntime runtime, IntPtr charPtr, IntPtr servicePtr, int x, int y)
    {
        var charLayout = runtime.Layout.Char;
        int safeY = Math.Max(MapSnapshot.TileSize, y);
        if (charLayout.CySend >= 0)
        {
            runtime.WriteField(charPtr, charLayout.CySend, safeY - 1);
        }

        runtime.WriteField(charPtr, charLayout.X, x);
        runtime.WriteField(charPtr, charLayout.Y, safeY);
        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, servicePtr);
    }

    private static async Task SendNormalMoveAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int targetX,
        int targetY)
    {
        var charLayout = runtime.Layout.Char;
        ResetMoveState(runtime, charPtr, 2);
        SendCharMovePacket(runtime, charPtr, servicePtr, targetX, targetY);
        runtime.WriteField(charPtr, charLayout.StatusMe, 1);
        await Task.Yield();
    }

    private static int GetWaypointTargetX(WorldWaypointInfo wp, int mapPixelWidth)
    {
        if (wp.MaxX < 60)
        {
            return Math.Clamp(15, (int)wp.MinX, (int)wp.MaxX);
        }

        if ((mapPixelWidth > 0 && wp.MinX > mapPixelWidth - 60 && wp.MaxX <= mapPixelWidth + 60) ||
            (!wp.IsEnter && wp.MinX >= 60))
        {
            int target = mapPixelWidth > 0 && Math.Abs(wp.MaxX - mapPixelWidth) <= 60
                ? mapPixelWidth - 15
                : wp.MaxX - 15;
            return Math.Clamp(target, (int)wp.MinX, (int)wp.MaxX);
        }

        return wp.MinX + (wp.MaxX - wp.MinX) / 2;
    }

    private static async Task<Result> StepToAirMapBottomAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int currentX,
        int mapPixelWidth,
        int mapPixelHeight,
        CancellationToken cancellationToken = default,
        MapSnapshot? map = null)
    {
        int targetX = Math.Max(48, Math.Min(mapPixelWidth - 48, currentX));
        int targetY = mapPixelHeight - MapSnapshot.TileSize;

        await TeleportToAsync(runtime, charPtr, servicePtr, targetX, targetY, cancellationToken, map);
        await Task.Delay(100, cancellationToken);

        await SendNormalMoveAsync(runtime, charPtr, servicePtr, targetX, mapPixelHeight - 12);
        await Task.Delay(60, cancellationToken);

        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodRequestChangeMap, servicePtr);
        return Result.Success();
    }

    private static WorldNpcInfo? FindTransitionNpc(int currMapId, int nextMapId, IReadOnlyList<WorldNpcInfo> npcs)
    {
        if (npcs == null || npcs.Count == 0) return null;

        if (WorldMapGraph.NpcActions.TryGetValue((currMapId, nextMapId), out var action))
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                if (npcs[i].TemplateId == action.NpcTemplateId) return npcs[i];
            }
        }

        return null;
    }

    private static async Task<Result> UseSpaceShipAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        WorldNpcInfo shipNpc,
        int currentMapId,
        int targetPlanetMapId,
        bool teleportToNpc = true,
        CancellationToken cancellationToken = default,
        MapSnapshot? map = null)
    {
        var (npcX, npcY) = map is { IsValid: true }
            ? map.ResolveSafePosition(shipNpc.X, shipNpc.Y, preferGround: true)
            : (shipNpc.X, shipNpc.Y > MapSnapshot.TileSize ? (shipNpc.Y / MapSnapshot.TileSize) * MapSnapshot.TileSize : shipNpc.Y);

        if (teleportToNpc)
        {
            await TeleportToAsync(runtime, charPtr, servicePtr, npcX, npcY, cancellationToken, map);
            await Task.Delay(180, cancellationToken);
        }

        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodOpenMenu, servicePtr, shipNpc.TemplateId);
        await Task.Delay(400, cancellationToken);

        sbyte menuIndex = 0;
        int[]? selectChain = null;

        if (WorldMapGraph.NpcActions.TryGetValue((currentMapId, targetPlanetMapId), out var npcAction))
        {
            menuIndex = npcAction.Select;
            selectChain = npcAction.SelectChain;
        }

        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodConfirmMenu, servicePtr, (short)shipNpc.TemplateId, menuIndex);

        if (selectChain != null)
        {
            foreach (int chainIndex in selectChain)
            {
                await Task.Delay(350, cancellationToken);
                runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodConfirmMenu, servicePtr, (short)shipNpc.TemplateId, (sbyte)chainIndex);
            }
        }

        return Result.Success();
    }
}
