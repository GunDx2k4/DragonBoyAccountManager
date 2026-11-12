using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public static class WorldMapWaypointExecutor
{
    public static bool IsAirMapDownwardTransition(int currMapId, int nextMapId)
    {
        return (currMapId == 46 && nextMapId == 47) ||
               (currMapId == 45 && nextMapId == 46) ||
               (currMapId == 48 && nextMapId == 45);
    }

    public static void ResetMoveState(IGameRuntime runtime, IntPtr charPtr, int statusMe)
    {
        var charLayout = runtime.Layout.Char;
        runtime.WriteField(charPtr, charLayout.CurrentMovePoint, IntPtr.Zero);
        runtime.WriteField(charPtr, charLayout.Cvx, 0);
        runtime.WriteField(charPtr, charLayout.Cvy, 0);
        runtime.WriteField(charPtr, charLayout.StatusMe, statusMe);
        if (charLayout.IsTeleport >= 0)
        {
            runtime.WriteField(charPtr, charLayout.IsTeleport, false);
        }
    }

    public static async Task TeleportToAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int targetX,
        int targetY,
        CancellationToken cancellationToken = default)
    {
        var charLayout = runtime.Layout.Char;
        ResetMoveState(runtime, charPtr, 1);

        runtime.WriteField(charPtr, charLayout.X, targetX);
        runtime.WriteField(charPtr, charLayout.Y, targetY);
        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, servicePtr);

        runtime.WriteField(charPtr, charLayout.Y, targetY + 1);
        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, servicePtr);

        runtime.WriteField(charPtr, charLayout.Y, targetY);
        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, servicePtr);

        await Task.Yield();
    }

    public static async Task SendNormalMoveAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int targetX,
        int targetY,
        CancellationToken cancellationToken = default)
    {
        var charLayout = runtime.Layout.Char;
        ResetMoveState(runtime, charPtr, 2);

        runtime.WriteField(charPtr, charLayout.X, targetX);
        runtime.WriteField(charPtr, charLayout.Y, targetY);
        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, servicePtr);

        runtime.WriteField(charPtr, charLayout.StatusMe, 1);
        await Task.Yield();
    }

    public static int GetWaypointTargetX(WorldWaypointInfo wp, int mapPixelWidth)
    {
        int minX = wp.MinX;
        int maxX = wp.MaxX;
        int center = (minX + maxX) / 2;

        if (maxX <= 60)
        {
            if (15 >= minX && 15 <= maxX)
            {
                return 15;
            }
            return Math.Max(6, center);
        }

        if (minX >= mapPixelWidth - 60)
        {
            int rightTarget = mapPixelWidth - 15;
            if (rightTarget >= minX && rightTarget <= maxX)
            {
                return rightTarget;
            }
            return Math.Min(mapPixelWidth - 6, center);
        }

        return center;
    }

    public static int GetWaypointApproachX(WorldWaypointInfo wp, int targetX, int targetY, int mapPixelWidth, bool isAirOrTower)
    {
        bool isLeftEdge = wp.MaxX <= 60;
        bool isRightEdge = !isLeftEdge && wp.MinX >= mapPixelWidth - 60;

        if (!isLeftEdge && !isRightEdge)
        {
            return targetX;
        }

        if (isLeftEdge)
        {
            return Math.Max(35, targetX + 20);
        }

        return Math.Min(mapPixelWidth - 35, targetX - 20);
    }

    public static int GetWaypointTargetY(WorldWaypointInfo wp, bool isAirOrTower)
    {
        if (isAirOrTower)
        {
            return (wp.MaxY > wp.MinY + 24) ? ((wp.MinY + wp.MaxY) / 2) : wp.MaxY;
        }

        return wp.MaxY > 0 ? wp.MaxY : wp.Y;
    }

    public static async Task<Result> StepToWaypointAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        WorldWaypointInfo wp,
        int mapPixelWidth,
        int currentMapId,
        CancellationToken cancellationToken = default)
    {
        bool isAirOrTower = currentMapId == 45 || currentMapId == 46 || currentMapId == 47 || currentMapId == 48;
        int wpTargetX = GetWaypointTargetX(wp, mapPixelWidth);
        int wpTargetY = GetWaypointTargetY(wp, isAirOrTower);
        int approachX = GetWaypointApproachX(wp, wpTargetX, wpTargetY, mapPixelWidth, isAirOrTower);

        await ExecuteTeleportAndChangeMapAsync(
            runtime,
            charPtr,
            servicePtr,
            wpTargetX,
            wpTargetY,
            approachX,
            wp.IsOffline,
            cancellationToken);

        return Result.Success();
    }

    public static async Task ExecuteTeleportAndChangeMapAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int targetX,
        int targetY,
        int approachX,
        bool isOffline,
        CancellationToken cancellationToken = default)
    {
        if (approachX != targetX)
        {
            await TeleportToAsync(runtime, charPtr, servicePtr, approachX, targetY, cancellationToken);
            await Task.Delay(100, cancellationToken);
            runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, servicePtr);
            await Task.Delay(50, cancellationToken);
        }

        await TeleportToAsync(runtime, charPtr, servicePtr, targetX, targetY, cancellationToken);
        await Task.Delay(80, cancellationToken);

        await SendNormalMoveAsync(runtime, charPtr, servicePtr, targetX, targetY, cancellationToken);
        await Task.Delay(60, cancellationToken);

        string changeMapMethod = isOffline ? GameSchema.Service.MethodGetMapOffline : GameSchema.Service.MethodRequestChangeMap;
        runtime.CallMethod(GameSchema.Service.ClassName, changeMapMethod, servicePtr);
    }

    public static async Task<Result> StepToAirMapBottomAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        int currentX,
        int mapPixelWidth,
        int mapPixelHeight,
        CancellationToken cancellationToken = default)
    {
        int targetX = Math.Max(48, Math.Min(mapPixelWidth - 48, currentX));
        int targetY = mapPixelHeight - 24;

        await TeleportToAsync(runtime, charPtr, servicePtr, targetX, targetY, cancellationToken);
        await Task.Delay(100, cancellationToken);

        await SendNormalMoveAsync(runtime, charPtr, servicePtr, targetX, mapPixelHeight - 12, cancellationToken);
        await Task.Delay(60, cancellationToken);

        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodRequestChangeMap, servicePtr);
        return Result.Success();
    }

    public static int GetFallbackNpcMenuIndex(int currentMapId, int targetMapId) =>
        WorldMapData.NpcActions.TryGetValue((currentMapId, targetMapId), out var npcAction) ? npcAction.Select : 0;

    public static WorldNpcInfo? FindTransitionNpc(int currMapId, int nextMapId, IReadOnlyList<WorldNpcInfo> npcs)
    {
        if (npcs == null || npcs.Count == 0) return null;

        if (WorldMapData.NpcActions.TryGetValue((currMapId, nextMapId), out var action))
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                if (npcs[i].TemplateId == action.NpcTemplateId) return npcs[i];
            }
        }

        return null;
    }

    public static async Task<Result> UseSpaceShipAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        WorldNpcInfo shipNpc,
        int currentMapId,
        int targetPlanetMapId,
        bool teleportToNpc = true,
        CancellationToken cancellationToken = default)
    {
        int npcX = shipNpc.X;
        int npcY = shipNpc.Y - 3;

        if (teleportToNpc)
        {
            await TeleportToAsync(runtime, charPtr, servicePtr, npcX, npcY, cancellationToken);
            await Task.Delay(180, cancellationToken);
        }

        runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodOpenMenu, servicePtr, shipNpc.TemplateId);
        await Task.Delay(400, cancellationToken);

        sbyte menuIndex = (sbyte)GetFallbackNpcMenuIndex(currentMapId, targetPlanetMapId);
        int[]? selectChain = null;

        if (WorldMapData.NpcActions.TryGetValue((currentMapId, targetPlanetMapId), out var npcAction))
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

    public static WorldWaypointInfo? FindWaypointToMap(
        int currMapId,
        int planetId,
        int nextMapId,
        IReadOnlyList<WorldWaypointInfo> waypoints,
        int mapPixelWidth)
    {
        if (waypoints == null || waypoints.Count == 0) return null;

        for (int j = 0; j < waypoints.Count; j++)
        {
            var wp = waypoints[j];
            int resolvedId = WorldMapData.ResolveWaypointTargetMapId(wp.Name, currMapId, planetId);
            if (resolvedId == nextMapId)
            {
                return wp;
            }
        }

        for (int j = 0; j < waypoints.Count; j++)
        {
            var wp = waypoints[j];
            if (!string.IsNullOrEmpty(wp.Name) && WorldMapData.MatchMapId(wp.Name, nextMapId))
            {
                return wp;
            }
        }

        if (nextMapId == 21 || nextMapId == 22 || nextMapId == 23)
        {
            for (int j = 0; j < waypoints.Count; j++)
            {
                var wp = waypoints[j];
                if (wp.IsOffline || (wp.IsEnter && wp.MinX >= 60 && wp.MaxX <= mapPixelWidth - 60))
                {
                    return wp;
                }
            }
            return null;
        }

        if (currMapId == 21 || currMapId == 22 || currMapId == 23)
        {
            return waypoints[0];
        }

        if ((currMapId == 0 || currMapId == 7 || currMapId == 14) &&
            (nextMapId == 42 || nextMapId == 43 || nextMapId == 44 || nextMapId == 39 || nextMapId == 40 || nextMapId == 41))
        {
            for (int j = 0; j < waypoints.Count; j++)
            {
                var wp = waypoints[j];
                if (!wp.IsOffline && !wp.IsEnter && wp.MaxX < 100)
                {
                    return wp;
                }
            }
        }

        if (currMapId == 42)
        {
            if (nextMapId == 47) return waypoints.OrderBy(w => w.MinX).FirstOrDefault();
            if (nextMapId == 0) return waypoints.OrderByDescending(w => w.MaxX).FirstOrDefault();
        }
        else if (currMapId == 47)
        {
            if (nextMapId == 46) return waypoints.OrderBy(w => w.MinY).FirstOrDefault();
            if (nextMapId == 42) return waypoints.OrderBy(w => w.MinX).FirstOrDefault();
            if (nextMapId == 2) return waypoints.OrderByDescending(w => w.MaxX).FirstOrDefault();
        }
        else if (currMapId == 46)
        {
            if (nextMapId == 45) return waypoints.Where(w => w.MinY <= 150).OrderBy(w => w.MinY).FirstOrDefault();
            if (nextMapId == 47) return waypoints.FirstOrDefault(w => w.MinY > 150);
        }
        else if (currMapId == 45)
        {
            if (nextMapId == 48) return waypoints.Where(w => w.MinY <= 150).OrderBy(w => w.MinY).FirstOrDefault();
            if (nextMapId == 46) return waypoints.FirstOrDefault(w => w.MinY > 150);
        }
        else if (currMapId == 48 && nextMapId == 45)
        {
            return waypoints.FirstOrDefault(w => w.MinY > 150);
        }

        int chainSide = WorldMapData.GetChainWaypointSide(currMapId, nextMapId);
        int preferSide = chainSide != 0 ? chainSide : (nextMapId > currMapId ? 1 : -1);

        if (preferSide > 0)
        {
            return waypoints
                .Where(w => WorldMapData.ResolveWaypointTargetMapId(w.Name, currMapId, planetId) == -1)
                .OrderByDescending(w => w.MaxX)
                .FirstOrDefault();
        }

        return waypoints
            .Where(w => WorldMapData.ResolveWaypointTargetMapId(w.Name, currMapId, planetId) == -1)
            .OrderBy(w => w.MinX)
            .FirstOrDefault();
    }
}
