using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
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

        session.Tick();
        if (session.CurrentWorld.MapId == targetMapId)
        {
            return Result.Success($"Đã ở map đích #{targetMapId} ({WorldMapData.GetMapName(targetMapId)}).");
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

            int planetId = WorldMapData.GetPlanetId(currentMapId, session.CurrentPlayer.Gender);
            WorldMapData.SyncLiveWaypoints(currentMapId, planetId, session.CurrentWorld.Waypoints);

            var path = WorldMapPathFinder.FindPath(
                currentMapId,
                targetMapId,
                session.CurrentPlayer.Gender,
                -1,
                session.CurrentPlayer.Power,
                true);

            if (path == null || path.Count == 0)
            {
                return Result.Failure($"Không tìm thấy đường từ {WorldMapData.GetMapName(currentMapId)} (Map #{currentMapId}) đến {WorldMapData.GetMapName(targetMapId)} (Map #{targetMapId}).");
            }

            int nextMapId = path[0];
            string nextMapName = WorldMapData.GetMapName(nextMapId);
            bool isNpcHop = WorldMapData.IsNpcTransitionEdge(currentMapId, nextMapId) ||
                            WorldMapData.NpcActions.ContainsKey((currentMapId, nextMapId));

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

                var hopExecResult = await ExecuteHopAsync(session, currentMapId, nextMapId, isNpcHop, isFastTeleport, progress, cancellationToken);
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

                bool mapChanged = await WaitForMapChangeAsync(session, startMapId, isNpcHop, cancellationToken);
                if (mapChanged)
                {
                    hopSuccess = true;
                    WorldMapGraph.Instance.RegisterDynamicConnection(startMapId, session.CurrentWorld.MapId, session.CurrentWorld.MapName);
                    break;
                }

                hopRetries++;
            }

            if (!hopSuccess)
            {
                WorldMapData.RemoveNeighbor(startMapId, nextMapId);
                return Result.Failure($"Không thể chuyển từ Map #{startMapId} sang {nextMapName} sau {MaxRetriesPerHop} lần thử.");
            }

            transitionsCount++;
            await Task.Delay(150, cancellationToken);
        }

        session.Tick();
        if (session.CurrentWorld.MapId == targetMapId)
        {
            return Result.Success($"Đã tới {session.CurrentWorld.MapName} (Map #{targetMapId})");
        }

        return Result.Failure($"Vượt quá số lần chuyển map tối đa ({MaxTransitions}) mà chưa tới đích.");
    }

    private static async Task<Result> ExecuteHopAsync(
        ISessionContext session,
        int currentMapId,
        int nextMapId,
        bool isNpcHop,
        bool isFastTeleport,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var runtime = session.Runtime;
        IntPtr charPtr = runtime.GetMyCharPtr();
        IntPtr service = runtime.GetServicePtr();

        if (charPtr == IntPtr.Zero || service == IntPtr.Zero)
            return Result.Failure("Không tìm thấy con trỏ nhân vật hoặc Service.");

        int mapPixelWidth = session.CurrentMap.Width > 0 ? session.CurrentMap.Width * 24 : 2400;
        int mapPixelHeight = session.CurrentMap.Height > 0 ? session.CurrentMap.Height * 24 : 1000;

        if (isNpcHop)
        {
            var transitionNpc = WorldMapWaypointExecutor.FindTransitionNpc(currentMapId, nextMapId, session.CurrentWorld.Npcs);
            if (transitionNpc != null && transitionNpc.X > 0 && transitionNpc.Y > 0)
            {
                await WorldMapWaypointExecutor.UseSpaceShipAsync(runtime, charPtr, service, transitionNpc, currentMapId, nextMapId, cancellationToken);
                return Result.Success();
            }

            return Result.Failure($"Không tìm thấy NPC chuyển map từ #{currentMapId} sang #{nextMapId}.");
        }

        if (WorldMapWaypointExecutor.IsAirMapDownwardTransition(currentMapId, nextMapId))
        {
            return await WorldMapWaypointExecutor.StepToAirMapBottomAsync(
                runtime,
                charPtr,
                service,
                session.CurrentPlayer.X,
                mapPixelWidth,
                mapPixelHeight,
                cancellationToken);
        }

        int planetId = WorldMapData.GetPlanetId(currentMapId, session.CurrentPlayer.Gender);
        WorldWaypointInfo? matchedWp = null;
        int waitTimeMs = 0;
        const int maxWaitWpMs = 2000;
        const int pollIntervalMs = 100;

        while (waitTimeMs < maxWaitWpMs)
        {
            if (cancellationToken.IsCancellationRequested)
                return Result.Failure("Quá trình Xmap bị hủy.");

            matchedWp = WorldMapWaypointExecutor.FindWaypointToMap(
                currentMapId,
                planetId,
                nextMapId,
                session.CurrentWorld.Waypoints,
                mapPixelWidth);

            if (matchedWp != null)
            {
                break;
            }

            await Task.Delay(pollIntervalMs, cancellationToken);
            waitTimeMs += pollIntervalMs;
            session.Tick();
        }

        if (matchedWp == null)
        {
            return Result.Failure($"Chưa tải được tọa độ cổng từ #{currentMapId} sang #{nextMapId} ({WorldMapData.GetMapName(nextMapId)}).");
        }

        WorldMapData.SyncLiveWaypoints(currentMapId, planetId, session.CurrentWorld.Waypoints);

        return await WorldMapWaypointExecutor.StepToWaypointAsync(
            runtime,
            charPtr,
            service,
            matchedWp,
            mapPixelWidth,
            currentMapId,
            cancellationToken);
    }

    private static async Task<bool> WaitForMapChangeAsync(
        ISessionContext session,
        int originalMapId,
        bool isNpcHop,
        CancellationToken cancellationToken)
    {
        int baseTimeoutMs = isNpcHop ? 7000 : 4000;
        int maxWaitTimeMs = baseTimeoutMs;
        int elapsedMs = 0;
        const int pollIntervalMs = 80;

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
                await Task.Delay(200, cancellationToken);
                session.Tick();
                return true;
            }

            if (session.CurrentPlayer.IsChangingMap)
            {
                if (maxWaitTimeMs < baseTimeoutMs + 6000)
                {
                    maxWaitTimeMs = baseTimeoutMs + 6000;
                }
            }
        }

        return false;
    }
}
