using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Algorithms;
using DragonBoyAccountManager.Core.Game.Schema;

namespace DragonBoyAccountManager.Core.Game.Actions;

public class AutoMoveAction(int targetX, int targetY, bool isFastTeleport = false)
    : GameAction($"Auto Move to ({targetX}, {targetY})")
{
    public int TargetX => targetX;
    public int TargetY => targetY;
    public bool IsFastTeleport => isFastTeleport;

    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!context.IsRunning || !context.Player.IsInGame)
            {
                progress?.Report("Game chưa sẵn sàng hoặc chưa đăng nhập.");
                return ActionResult.Failure("Game chưa sẵn sàng hoặc chưa đăng nhập.");
            }

            IntPtr myCharPtr = context.Player.Pointer;
            if (myCharPtr == IntPtr.Zero)
            {
                progress?.Report("Không tìm thấy con trỏ nhân vật myChar.");
                return ActionResult.Failure("Không tìm thấy con trỏ nhân vật myChar.");
            }

            int startMapId = context.World.MapId;
            if (context.Player.IsChangingMap)
            {
                ClearMoveTarget(context, myCharPtr);
                progress?.Report("Đang đổi map, dừng lộ trình di chuyển.");
                return ActionResult.Success("Đang đổi map, dừng lộ trình di chuyển.");
            }

            int curX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
            int curY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);

            int effectiveTargetY = TargetY <= 0 ? context.World.GetYGround(TargetX) : TargetY;

            if (Math.Abs(curX - TargetX) <= 15 && Math.Abs(curY - effectiveTargetY) <= 15)
            {
                progress?.Report("Đã ở vị trí mục tiêu.");
                return ActionResult.Success("Đã ở vị trí mục tiêu.");
            }

            if (IsFastTeleport)
            {
                return await ExecuteFastTeleportAsync(context, myCharPtr, effectiveTargetY, startMapId, progress, cancellationToken);
            }

            return await ExecuteAStarMovementAsync(context, myCharPtr, curX, curY, effectiveTargetY, startMapId, progress, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            progress?.Report("Quá trình di chuyển bị hủy bỏ.");
            return ActionResult.Failure("Quá trình di chuyển bị hủy bỏ.");
        }
        catch (Exception ex)
        {
            progress?.Report($"Lỗi khi di chuyển: {ex.Message}");
            return ActionResult.Failure(ex.Message);
        }
    }

    private async Task<ActionResult> ExecuteFastTeleportAsync(
        IGameContext context,
        IntPtr myCharPtr,
        int destY,
        int startMapId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
        {
            ClearMoveTarget(context, myCharPtr);
            progress?.Report("Đang đổi map, dừng lộ trình di chuyển.");
            return ActionResult.Success("Đang đổi map hoặc đã sang map mới, dừng lộ trình di chuyển.");
        }

        progress?.Report($"Dịch chuyển nhanh tới ({TargetX}, {destY})...");

        ClearMoveTarget(context, myCharPtr);

        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, TargetX);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, destY);
        context.Network.CharMove();

        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, TargetX);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, destY + 1);
        context.Network.CharMove();

        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, TargetX);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, destY);
        context.Network.CharMove();

        ClearMoveTarget(context, myCharPtr);
        progress?.Report($"Pos: {TargetX}, {destY}");

        await Task.CompletedTask;
        return ActionResult.Success($"Đã dịch chuyển nhanh tới ({TargetX}, {destY}).");
    }

    private async Task<ActionResult> ExecuteAStarMovementAsync(
        IGameContext context,
        IntPtr myCharPtr,
        int curX,
        int curY,
        int destY,
        int startMapId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        (int Width, int Height, int[] Types)? mapData = null;
        for (int i = 0; i < 3; i++)
        {
            mapData = context.World.GetCollisionData();
            if (mapData != null) break;
            await Task.Delay(50, cancellationToken);
        }

        int tmw = mapData?.Width ?? 0;
        int tmh = mapData?.Height ?? 0;
        int[]? types = mapData?.Types;

        List<(int X, int Y)> waypoints = [];
        if (types != null && tmw > 0 && tmh > 0)
        {
            progress?.Report($"Đang tính toán đường đi A* tới ({TargetX}, {destY})...");
            waypoints = TileAStarFinder.FindPath(curX, curY, TargetX, destY, tmw, tmh, types);
        }

        if (waypoints.Count <= 1)
        {
            waypoints = [(curX, curY), (TargetX, destY)];
        }

        progress?.Report($"Đi theo lộ trình ({waypoints.Count} mốc)...");

        for (int i = 1; i < waypoints.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                return ActionResult.Failure("Bị hủy hoặc game đã tắt.");

            if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
            {
                ClearMoveTarget(context, myCharPtr);
                progress?.Report("Đang đổi map, dừng lộ trình di chuyển.");
                return ActionResult.Success("Đang đổi map hoặc đã sang map mới, dừng lộ trình di chuyển.");
            }

            var wp = waypoints[i];
            bool isLast = (i == waypoints.Count - 1);
            int threshold = isLast ? 16 : 24;

            bool stepOk = await MoveToPointAsync(context, myCharPtr, wp.X, wp.Y, threshold, tmw, tmh, types, startMapId, cancellationToken, progress);

            if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
            {
                ClearMoveTarget(context, myCharPtr);
                progress?.Report("Đang đổi map, dừng lộ trình di chuyển.");
                return ActionResult.Success("Đang đổi map hoặc đã sang map mới, dừng lộ trình di chuyển.");
            }

            if (!stepOk && !isLast && types != null)
            {
                int nowX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
                int nowY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);
                var recomputed = TileAStarFinder.FindPath(nowX, nowY, TargetX, destY, tmw, tmh, types);
                if (recomputed.Count > 1)
                {
                    waypoints = recomputed;
                    i = 0;
                }
            }
        }

        ClearMoveTarget(context, myCharPtr);

        if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
        {
            progress?.Report("Đang đổi map, dừng lộ trình di chuyển.");
            return ActionResult.Success("Đang đổi map hoặc đã sang map mới, dừng lộ trình di chuyển.");
        }

        context.Network.CharMove();

        int finalX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
        int finalY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);
        progress?.Report($"Đã đến vị trí ({finalX}, {finalY}) thành công.");
        return ActionResult.Success($"Đã đến vị trí ({finalX}, {finalY}) thành công.");
    }

    private static async Task<bool> MoveToPointAsync(
        IGameContext context,
        IntPtr myCharPtr,
        int targetX,
        int targetY,
        int targetDist,
        int tmw,
        int tmh,
        int[]? types,
        int startMapId,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        try
        {
            if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
            {
                ClearMoveTarget(context, myCharPtr);
                return true;
            }

            SetNativeTarget(context, myCharPtr, targetX, targetY);

            int initialY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);
            if (targetY < initialY - 12)
            {
                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, -10);
                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 3);
            }

            await Task.Delay(40, cancellationToken);

            int anchorX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
            int anchorY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);
            var stuckSw = System.Diagnostics.Stopwatch.StartNew();
            var reportSw = System.Diagnostics.Stopwatch.StartNew();
            int unstuckAttempts = 0;

            while (!cancellationToken.IsCancellationRequested && context.IsRunning)
            {
                if (context.Player.Hp <= 0)
                    return false;

                if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
                {
                    ClearMoveTarget(context, myCharPtr);
                    return true;
                }

                int curX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
                int curY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);

                if (reportSw.ElapsedMilliseconds >= 200)
                {
                    progress?.Report($"Pos: {curX}, {curY}");
                    reportSw.Restart();
                }

                int dist = Math.Abs(curX - targetX) + Math.Abs(curY - targetY);
                if (dist <= targetDist)
                {
                    progress?.Report($"Pos: {curX}, {curY}");
                    return true;
                }

                IntPtr cmp = context.Reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, myCharPtr);
                if (cmp == IntPtr.Zero)
                {
                    if (Math.Abs(curX - targetX) <= targetDist && Math.Abs(curY - targetY) <= targetDist)
                    {
                        progress?.Report($"Pos: {curX}, {curY}");
                        return true;
                    }

                    if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
                    {
                        ClearMoveTarget(context, myCharPtr);
                        return true;
                    }

                    SetNativeTarget(context, myCharPtr, targetX, targetY);
                    if (targetY < curY - 12)
                    {
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, -10);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 3);
                    }
                }

                int distFromAnchor = Math.Abs(curX - anchorX) + Math.Abs(curY - anchorY);
                if (distFromAnchor > 24)
                {
                    stuckSw.Restart();
                    anchorX = curX;
                    anchorY = curY;
                    unstuckAttempts = 0;
                }
                else if (stuckSw.ElapsedMilliseconds >= 2000)
                {
                    unstuckAttempts++;
                    if (unstuckAttempts > 4)
                        return false;

                    await HandleUnstuckAsync(context, myCharPtr, curX, curY, targetX, targetY, unstuckAttempts, tmw, tmh, types, progress, cancellationToken);
                    anchorX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
                    anchorY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);
                    stuckSw.Restart();
                }

                await Task.Delay(40, cancellationToken);
            }

            if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
            {
                ClearMoveTarget(context, myCharPtr);
                return true;
            }

            int finalX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
            return Math.Abs(finalX - targetX) <= targetDist;
        }
        catch (OperationCanceledException)
        {
            ClearMoveTarget(context, myCharPtr);
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static async Task HandleUnstuckAsync(
        IGameContext context,
        IntPtr myCharPtr,
        int curX,
        int curY,
        int targetX,
        int targetY,
        int unstuckAttempts,
        int tmw,
        int tmh,
        int[]? types,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        int liftDistance = 64 + (unstuckAttempts * 24);
        bool canFlyUp = CanFlyUp(curX, curY, liftDistance, tmw, tmh, types);
        int escapeY;

        if (canFlyUp)
        {
            escapeY = Math.Max(24, curY - liftDistance);
            progress?.Report($"Kẹt góc >2s tại ({curX}, {curY}), bay vút lên cao (Y={escapeY}) để thoát kẹp...");

            context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, -10);
            context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 3);
            SetNativeTarget(context, myCharPtr, curX, escapeY);
        }
        else if (CanDrop(curX, curY, 48, tmw, tmh, types))
        {
            escapeY = curY + 48;
            progress?.Report($"Kẹt góc >2s tại ({curX}, {curY}), trần cản -> hạ thấp (Y={escapeY}) để thoát...");
            SetNativeTarget(context, myCharPtr, curX, escapeY);
        }
        else
        {
            int stepBackX = curX + (targetX >= curX ? -32 : 32);
            progress?.Report($"Kẹt góc hẹp >2s tại ({curX}, {curY}), lùi nhẹ ({stepBackX}) lấy đà...");
            SetNativeTarget(context, myCharPtr, stepBackX, curY);
            escapeY = curY;
        }

        var liftSw = System.Diagnostics.Stopwatch.StartNew();
        while (liftSw.ElapsedMilliseconds < 600 && !cancellationToken.IsCancellationRequested && context.IsRunning)
        {
            await Task.Delay(40, cancellationToken);
            int checkY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);
            if (Math.Abs(checkY - escapeY) <= 20)
                break;
        }

        int nowX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
        if (Math.Abs(nowX - targetX) > 24)
        {
            SetNativeTarget(context, myCharPtr, targetX, escapeY);
        }
        else
        {
            SetNativeTarget(context, myCharPtr, targetX, targetY);
        }
    }

    private static void ClearMoveTarget(IGameContext context, IntPtr myCharPtr)
    {
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, myCharPtr, IntPtr.Zero);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldEndMovePointCommand, myCharPtr, IntPtr.Zero);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvx, myCharPtr, 0);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, 0);
        context.World.ClickMoving = false;
    }

    private static bool SetNativeTarget(IGameContext context, IntPtr myCharPtr, int targetX, int targetY)
    {
        try
        {
            IntPtr mp = context.Reflector.CreateInstance(GameSchema.MovePoint.ClassName, targetX, targetY);
            if (mp != IntPtr.Zero)
            {
                int curX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
                context.Reflector.WriteField(GameSchema.MovePoint.ClassName, GameSchema.MovePoint.FieldXEnd, mp, targetX);
                context.Reflector.WriteField(GameSchema.MovePoint.ClassName, GameSchema.MovePoint.FieldYEnd, mp, targetY);
                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, myCharPtr, mp);
                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCdir, myCharPtr, targetX >= curX ? 1 : -1);
                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldEndMovePointCommand, myCharPtr, IntPtr.Zero);

                context.World.ClickMoving = true;
                return true;
            }

            context.World.CheckClickMoveTo(targetX, targetY);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanFlyUp(int curX, int curY, int checkHeight, int tmw, int tmh, int[]? types)
    {
        int targetY = Math.Max(24, curY - checkHeight);
        if (curY - targetY < 20)
            return false;

        if (types == null || tmw <= 0 || tmh <= 0)
            return true;

        int tx = Math.Clamp(curX / TileAStarFinder.TileSize, 0, tmw - 1);
        int startTy = Math.Clamp(targetY / TileAStarFinder.TileSize, 0, tmh - 1);
        int headTy = Math.Clamp((curY - 24) / TileAStarFinder.TileSize, 0, tmh - 1);

        for (int ty = startTy; ty <= headTy; ty++)
        {
            if (TileAStarFinder.IsWall(tx, ty, tmw, tmh, types))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CanDrop(int curX, int curY, int dropDistance, int tmw, int tmh, int[]? types)
    {
        if (types == null || tmw <= 0 || tmh <= 0)
            return false;

        int tx = Math.Clamp(curX / TileAStarFinder.TileSize, 0, tmw - 1);
        int targetDropY = curY + dropDistance;
        int checkTy = Math.Clamp(targetDropY / TileAStarFinder.TileSize, 0, tmh - 1);

        return targetDropY < (tmh - 1) * TileAStarFinder.TileSize && !TileAStarFinder.IsWall(tx, checkTy, tmw, tmh, types);
    }
}
