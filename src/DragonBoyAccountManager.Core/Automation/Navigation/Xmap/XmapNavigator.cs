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

        if (session.CurrentPlayer.IsChangingMap || !session.CurrentMap.IsValid || session.CurrentMap.MapId != session.CurrentWorld.MapId)
        {
            int initWaitMs = 0;
            while (initWaitMs < 3000 && (session.CurrentPlayer.IsChangingMap || !session.CurrentMap.IsValid))
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

                bool effectiveFastTeleport = isFastTeleport && hopRetries == 0;
                var hopExecResult = await ExecuteHopAsync(session, currentMapId, nextMapId, isNpcHop, effectiveFastTeleport, cancellationToken);
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
        MapSnapshot? map = null,
        bool isFastTeleport = false)
    {
        var charLayout = runtime.Layout.Char;
        int curStatus = runtime.ReadField<int>(charPtr, charLayout.StatusMe);
        int curX = runtime.ReadField<int>(charPtr, charLayout.X);
        int curY = runtime.ReadField<int>(charPtr, charLayout.Y);
        int sentX = charLayout.CxSend >= 0 ? runtime.ReadField<int>(charPtr, charLayout.CxSend) : curX;
        int sentY = charLayout.CySend >= 0 ? runtime.ReadField<int>(charPtr, charLayout.CySend) : curY;
        bool hasValidSent = sentX > 0 && sentY > 0;

        if (curStatus == 16 && hasValidSent)
        {
            curX = sentX;
            curY = sentY;
        }

        int safeY = Math.Max(MapSnapshot.TileSize, targetY);
        if (isFastTeleport)
        {
            ResetMoveState(runtime, charPtr, 1);
            SendCharMovePacket(runtime, charPtr, servicePtr, targetX, safeY, forceSend: true);
            SendCharMovePacket(runtime, charPtr, servicePtr, targetX, safeY + 1, forceSend: true);
            SendCharMovePacket(runtime, charPtr, servicePtr, targetX, safeY, forceSend: true);
            await Task.Yield();
            return;
        }

        int stepPx = GetStepPx(runtime, charPtr);
        const int stepDelayMs = 30;
        int lastStepX = curX;
        int lastSentX = hasValidSent ? sentX : curX;
        int lastSentY = Math.Max(MapSnapshot.TileSize, hasValidSent ? sentY : curY);

        if (map is { IsValid: true })
        {
            var path = AutoNavigator.FindPath(map, curX, curY, targetX, safeY);
            if (path.Count >= 2)
            {
                int prevX = path[0].X;
                int prevY = path[0].Y;
                for (int i = 1; i < path.Count; i++)
                {
                    var (wx, wy) = path[i];
                    int dx = wx - prevX;
                    int dy = wy - prevY;
                    double segDist = Math.Sqrt((double)dx * dx + (double)dy * dy);
                    int subSteps = Math.Max(1, (int)Math.Ceiling(segDist / stepPx));

                    for (int s = 1; s <= subSteps; s++)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        int stepX = prevX + dx * s / subSteps;
                        int stepY = prevY + dy * s / subSteps;
                        bool isSegmentEnd = s == subSteps;
                        SendStepMove(
                            runtime,
                            charPtr,
                            servicePtr,
                            stepX,
                            stepY,
                            isSegmentEnd,
                            ref lastStepX,
                            ref lastSentX,
                            ref lastSentY);

                        bool isFinal = i == path.Count - 1 && isSegmentEnd;
                        if (!isFinal)
                        {
                            await Task.Delay(stepDelayMs, cancellationToken);
                        }
                    }

                    prevX = wx;
                    prevY = wy;
                }

                await Task.Yield();
                return;
            }
        }

        int totalDx = targetX - curX;
        int totalDy = safeY - curY;
        double totalDist = Math.Sqrt((double)totalDx * totalDx + (double)totalDy * totalDy);
        int steps = Math.Clamp((int)Math.Ceiling(totalDist / stepPx), 1, 600);

        for (int i = 1; i < steps; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            int stepX = curX + totalDx * i / steps;
            int lerpedY = curY + totalDy * i / steps;
            int stepY = map is { IsValid: true } ? map.GetSafeStepY(stepX, lerpedY) : lerpedY;

            SendStepMove(
                runtime,
                charPtr,
                servicePtr,
                stepX,
                stepY,
                false,
                ref lastStepX,
                ref lastSentX,
                ref lastSentY);
            await Task.Delay(stepDelayMs, cancellationToken);
        }

        SendStepMove(
            runtime,
            charPtr,
            servicePtr,
            targetX,
            safeY,
            true,
            ref lastStepX,
            ref lastSentX,
            ref lastSentY);
        await Task.Yield();
    }

    public static async Task<Result> StepToWaypointAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        WorldWaypointInfo wp,
        int mapPixelWidth,
        CancellationToken cancellationToken = default,
        MapSnapshot? map = null,
        bool isFastTeleport = false)
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
            map,
            isFastTeleport);

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
        MapSnapshot? map = null,
        bool isFastTeleport = false)
    {
        int startMapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);

        await TeleportToAsync(runtime, charPtr, servicePtr, targetX, targetY, cancellationToken, map, isFastTeleport);
        await WaitOrTriggerChangeMapAsync(runtime, charPtr, servicePtr, startMapId, isOffline, cancellationToken);
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
                session.CurrentMap,
                isFastTeleport);
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
        return await StepToWaypointAsync(
            runtime,
            charPtr,
            service,
            matchedWp,
            finalPixelWidth,
            cancellationToken,
            session.CurrentMap,
            isFastTeleport);
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
                const int maxSettleMs = 6000;
                int expectedWps = NextMapCatalog.GetExpectedWaypointCount(currentMapId, session.CurrentPlayer.Gender);

                while (settleWaitMs < maxSettleMs)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    session.Tick();

                    bool isChanging = session.CurrentPlayer.IsChangingMap;
                    bool hasValidMap = session.CurrentMap.IsValid && session.CurrentMap.MapId == currentMapId;
                    bool hasPlayerPos = session.CurrentPlayer.X > 0 && session.CurrentPlayer.Y >= MapSnapshot.TileSize;
                    bool hasWaypoints = expectedWps <= 0 ||
                                        (session.CurrentWorld.Waypoints.Count >= expectedWps &&
                                         session.CurrentWorld.Waypoints.All(w => !string.IsNullOrWhiteSpace(w.Name)));

                    if (!isChanging && hasValidMap && hasPlayerPos && hasWaypoints)
                    {
                        break;
                    }

                    await Task.Delay(50, cancellationToken);
                    settleWaitMs += 50;
                }

                await Task.Delay(150, cancellationToken);
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

        var runtime = session.Runtime;
        int postTimeoutMapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);
        IntPtr postCharPtr = runtime.GetMyCharPtr();
        if (postTimeoutMapId == originalMapId && !IsMapActivelyLoadingOrTeleporting(runtime, postCharPtr))
        {
            runtime.WriteStatic(GameSchema.Char.ClassName, runtime.Layout.Char.IsChangingMap, false);
        }

        return false;
    }

    private static bool IsMapActivelyLoadingOrTeleporting(IGameRuntime runtime, IntPtr charPtr)
    {
        return runtime.ReadStatic<bool>(GameSchema.Char.ClassName, runtime.Layout.Char.IsLoadingMap) ||
               runtime.ReadStatic<bool>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.IsLoading) ||
               (runtime.Layout.Controller.IsStopReadMessage >= 0 &&
                runtime.ReadStatic<bool>(GameSchema.Controller.ClassName, runtime.Layout.Controller.IsStopReadMessage)) ||
               (charPtr != IntPtr.Zero && runtime.ReadField<bool>(charPtr, runtime.Layout.Char.IsTeleport));
    }

    private static bool IsGameTransitioningMap(IGameRuntime runtime, IntPtr charPtr)
    {
        return runtime.ReadStatic<bool>(GameSchema.Char.ClassName, runtime.Layout.Char.IsChangingMap) ||
               IsMapActivelyLoadingOrTeleporting(runtime, charPtr);
    }

    private static async Task WaitOrTriggerChangeMapAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int startMapId,
        bool isOffline,
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < 8; i++)
        {
            int nowMapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);
            if (nowMapId != startMapId || IsGameTransitioningMap(runtime, charPtr))
            {
                return;
            }

            await Task.Delay(30, cancellationToken);
        }

        int finalMapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);
        if (finalMapId != startMapId || IsGameTransitioningMap(runtime, charPtr))
        {
            return;
        }

        runtime.WriteStatic(GameSchema.Char.ClassName, runtime.Layout.Char.IsChangingMap, true);
        string changeMapMethod = isOffline ? GameSchema.Service.MethodGetMapOffline : GameSchema.Service.MethodRequestChangeMap;
        runtime.CallMethod(GameSchema.Service.ClassName, changeMapMethod, servicePtr);
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
        if (charLayout.TelePortSkill >= 0) runtime.WriteField(charPtr, charLayout.TelePortSkill, false);
        runtime.WriteField(charPtr, charLayout.StatusMe, statusMe);
    }

    private static int GetStepPx(IGameRuntime runtime, IntPtr charPtr)
    {
        var charLayout = runtime.Layout.Char;
        int cspeed = charLayout.Cspeed >= 0 ? runtime.ReadField<int>(charPtr, charLayout.Cspeed) : 4;
        int bonusSpeed = charLayout.CBonusSpeed >= 0 ? runtime.ReadField<int>(charPtr, charLayout.CBonusSpeed) : 0;
        int effectiveSpeed = Math.Clamp(cspeed + Math.Max(0, bonusSpeed), 4, 12);
        return Math.Clamp(effectiveSpeed * 3, 12, MapSnapshot.TileSize);
    }

    private static void SendCharMovePacket(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int x,
        int y,
        bool forceSend = false)
    {
        var charLayout = runtime.Layout.Char;
        int safeY = Math.Max(MapSnapshot.TileSize, y);
        if (charLayout.CySend >= 0)
        {
            if (forceSend || charLayout.CxSend < 0)
            {
                runtime.WriteField(charPtr, charLayout.CySend, safeY - 1);
            }
            else
            {
                int sentX = runtime.ReadField<int>(charPtr, charLayout.CxSend);
                int sentY = runtime.ReadField<int>(charPtr, charLayout.CySend);
                if (sentX == x && sentY == safeY)
                {
                    runtime.WriteField(charPtr, charLayout.CySend, safeY - 1);
                }
            }
        }

        runtime.WriteField(charPtr, charLayout.X, x);
        runtime.WriteField(charPtr, charLayout.Y, safeY);
        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, servicePtr);
    }

    private static void SendStepMove(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int stepX,
        int stepY,
        bool isMilestoneStep,
        ref int lastStepX,
        ref int lastSentX,
        ref int lastSentY)
    {
        var charLayout = runtime.Layout.Char;
        int safeStepY = Math.Max(MapSnapshot.TileSize, stepY);
        if (stepX != lastStepX && charLayout.Cdir >= 0)
        {
            runtime.WriteField(charPtr, charLayout.Cdir, stepX > lastStepX ? 1 : -1);
            lastStepX = stepX;
        }

        ResetMoveState(runtime, charPtr, 1);

        int deltaY = Math.Abs(safeStepY - lastSentY);
        int manhattanDelta = Math.Abs(stepX - lastSentX) + deltaY;
        bool shouldSendPacket = isMilestoneStep || deltaY >= MapSnapshot.TileSize || manhattanDelta >= 60;

        if (shouldSendPacket)
        {
            SendCharMovePacket(runtime, charPtr, servicePtr, stepX, safeStepY);
            lastSentX = stepX;
            lastSentY = safeStepY;
        }
        else
        {
            runtime.WriteField(charPtr, charLayout.X, stepX);
            runtime.WriteField(charPtr, charLayout.Y, safeStepY);
        }
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
        SendCharMovePacket(runtime, charPtr, servicePtr, targetX, targetY, forceSend: true);
        runtime.WriteField(charPtr, charLayout.StatusMe, 1);
        await Task.Yield();
    }

    private static int GetWaypointTargetX(WorldWaypointInfo wp, int mapPixelWidth)
    {
        int lo = Math.Min(wp.MinX, wp.MaxX);
        int hi = Math.Max(wp.MinX, wp.MaxX);

        if (hi < 60)
        {
            return Math.Clamp(18, lo, hi);
        }

        if (mapPixelWidth > 0 && hi >= mapPixelWidth - 60 && lo >= mapPixelWidth - 120)
        {
            int maxAllowed = Math.Max(lo, Math.Min(hi, mapPixelWidth - 12));
            int target = mapPixelWidth - 18;
            return Math.Clamp(target, lo, maxAllowed);
        }

        return lo + (hi - lo) / 2;
    }

    private static async Task<Result> StepToAirMapBottomAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int currentX,
        int mapPixelWidth,
        int mapPixelHeight,
        CancellationToken cancellationToken = default,
        MapSnapshot? map = null,
        bool isFastTeleport = false)
    {
        int targetX = Math.Max(48, Math.Min(mapPixelWidth - 48, currentX));
        int targetY = mapPixelHeight - MapSnapshot.TileSize;
        int startMapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);

        await TeleportToAsync(runtime, charPtr, servicePtr, targetX, targetY, cancellationToken, map, isFastTeleport);
        await Task.Delay(80, cancellationToken);

        await SendNormalMoveAsync(runtime, charPtr, servicePtr, targetX, mapPixelHeight - 12);
        await WaitOrTriggerChangeMapAsync(runtime, charPtr, servicePtr, startMapId, false, cancellationToken);
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
        bool isFastTeleport = false,
        CancellationToken cancellationToken = default,
        MapSnapshot? map = null)
    {
        var (npcX, npcY) = map is { IsValid: true }
            ? map.ResolveSafePosition(shipNpc.X, shipNpc.Y, preferGround: true)
            : (shipNpc.X, shipNpc.Y > MapSnapshot.TileSize ? (shipNpc.Y / MapSnapshot.TileSize) * MapSnapshot.TileSize : shipNpc.Y);

        await TeleportToAsync(runtime, charPtr, servicePtr, npcX, npcY, cancellationToken, map, isFastTeleport);
        await Task.Delay(180, cancellationToken);

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
