using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public static class WorldMapWaypointExecutor
{
    public static int GetMapPixelWidth(IGameRuntime runtime, int fallbackWidth = 0)
    {
        int pxw = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.Pxw);
        if (pxw > 0) return pxw;
        int tmw = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.Tmw);
        if (tmw > 0) return tmw * 24;
        return fallbackWidth > 0 ? fallbackWidth : 2400;
    }

    public static int GetMapPixelHeight(IGameRuntime runtime, int fallbackHeight = 0)
    {
        int pxh = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.Pxh);
        if (pxh > 0) return pxh;
        int tmh = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.Tmh);
        if (tmh > 0) return tmh * 24;
        return fallbackHeight > 0 ? fallbackHeight : 1000;
    }

    public static bool IsAirMapDownwardTransition(int currMapId, int nextMapId)
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

    public static int GetWaypointTargetY(WorldWaypointInfo wp)
    {
        return wp.MaxY > 0 ? wp.MaxY : wp.Y;
    }

    public static async Task<Result> StepToWaypointAsync(
        IGameRuntime runtime,
        IntPtr charPtr,
        IntPtr servicePtr,
        WorldWaypointInfo wp,
        int mapPixelWidth,
        CancellationToken cancellationToken = default)
    {
        int actualPxw = GetMapPixelWidth(runtime, mapPixelWidth);
        int wpTargetX = GetWaypointTargetX(wp, actualPxw);
        int wpTargetY = GetWaypointTargetY(wp);

        await ExecuteTeleportAndChangeMapAsync(
            runtime,
            charPtr,
            servicePtr,
            wpTargetX,
            wpTargetY,
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
        bool isOffline,
        CancellationToken cancellationToken = default)
    {
        int startMapId = runtime.ReadStatic<int>(GameSchema.TileMap.ClassName, runtime.Layout.TileMap.MapId);

        await TeleportToAsync(runtime, charPtr, servicePtr, targetX, targetY, cancellationToken);

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

        sbyte menuIndex = 0;
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
        int expectedWaypointCount = 0)
    {
        if (waypoints == null || waypoints.Count == 0) return null;
        if (expectedWaypointCount > 0 && waypoints.Count < expectedWaypointCount) return null;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(waypoints[i].Name)) return null;
        }

        string targetMapName = WorldMapData.GetMapName(nextMapId);

        for (int i = 0; i < waypoints.Count; i++)
        {
            var wp = waypoints[i];
            if (string.IsNullOrWhiteSpace(wp.Name)) continue;

            if (!string.IsNullOrEmpty(targetMapName) &&
                (wp.Name.Equals(targetMapName, StringComparison.OrdinalIgnoreCase) ||
                 WorldMapData.CleanName(wp.Name).Equals(WorldMapData.CleanName(targetMapName), StringComparison.OrdinalIgnoreCase)))
            {
                return wp;
            }

            if (WorldMapData.MatchMapId(wp.Name, nextMapId))
            {
                return wp;
            }

            int resolvedId = WorldMapData.ResolveWaypointTargetMapId(wp.Name, currMapId, planetId);
            if (resolvedId == nextMapId)
            {
                return wp;
            }
        }

        return null;
    }
}
