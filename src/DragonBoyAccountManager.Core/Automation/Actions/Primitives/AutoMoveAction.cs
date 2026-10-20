
namespace DragonBoyAccountManager.Core.Automation.Actions.Primitives;

public class AutoMoveAction(int targetX, int targetY, bool isFastTeleport = false)
    : GameAction($"Tự động di chuyển tới ({targetX}, {targetY})")
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
                progress?.Report("Trò chơi chưa sẵn sàng hoặc chưa đăng nhập.");
                return ActionResult.Failure("Trò chơi chưa sẵn sàng hoặc chưa đăng nhập.");
            }

            int startMapId = context.World.MapId;
            if (context.Player.IsChangingMap)
            {
                ClearMoveTarget(context);
                progress?.Report("Đang đổi bản đồ, dừng lộ trình di chuyển.");
                return ActionResult.Success("Đang đổi bản đồ, dừng lộ trình di chuyển.");
            }

            int curX = context.Player.X;
            int curY = context.Player.Y;

            int effectiveTargetY = TargetY <= 0 ? context.World.GetYGround(TargetX) : TargetY;

            if (Math.Abs(curX - TargetX) <= 15 && Math.Abs(curY - effectiveTargetY) <= 15)
            {
                progress?.Report("Đã ở vị trí mục tiêu.");
                return ActionResult.Success("Đã ở vị trí mục tiêu.");
            }

            if (IsFastTeleport)
            {
                return await ExecuteFastTeleportAsync(context, effectiveTargetY, startMapId, progress, cancellationToken);
            }

            return await ExecuteAStarMovementAsync(context, curX, curY, effectiveTargetY, startMapId, progress, cancellationToken);
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
        int destY,
        int startMapId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
        {
            ClearMoveTarget(context);
            progress?.Report("Đang đổi bản đồ, dừng lộ trình di chuyển.");
            return ActionResult.Success("Đang đổi bản đồ hoặc đã sang bản đồ mới, dừng lộ trình di chuyển.");
        }

        progress?.Report($"Dịch chuyển nhanh tới ({TargetX}, {destY})...");

        ClearMoveTarget(context);

        context.Player.SetPosition(TargetX, destY);
        context.Network.CharMove();

        context.Player.SetPosition(TargetX, destY + 1);
        context.Network.CharMove();

        context.Player.SetPosition(TargetX, destY);
        context.Network.CharMove();

        ClearMoveTarget(context);
        progress?.Report($"Vị trí: {TargetX}, {destY}");

        await Task.CompletedTask;
        return ActionResult.Success($"Đã dịch chuyển nhanh tới ({TargetX}, {destY}).");
    }

    private async Task<ActionResult> ExecuteAStarMovementAsync(
        IGameContext context,
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
                return ActionResult.Failure("Bị hủy hoặc trò chơi đã tắt.");

            if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
            {
                ClearMoveTarget(context);
                progress?.Report("Đang đổi bản đồ, dừng lộ trình di chuyển.");
                return ActionResult.Success("Đang đổi bản đồ hoặc đã sang bản đồ mới, dừng lộ trình di chuyển.");
            }

            var wp = waypoints[i];
            bool isLast = (i == waypoints.Count - 1);
            int threshold = isLast ? 16 : 24;

            bool stepOk = await MoveToPointAsync(context, wp.X, wp.Y, threshold, tmw, tmh, types, startMapId, cancellationToken, progress);

            if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
            {
                ClearMoveTarget(context);
                progress?.Report("Đang đổi bản đồ, dừng lộ trình di chuyển.");
                return ActionResult.Success("Đang đổi bản đồ hoặc đã sang bản đồ mới, dừng lộ trình di chuyển.");
            }

            if (!stepOk && !isLast && types != null)
            {
                int nowX = context.Player.X;
                int nowY = context.Player.Y;
                var recomputed = TileAStarFinder.FindPath(nowX, nowY, TargetX, destY, tmw, tmh, types);
                if (recomputed.Count > 1)
                {
                    waypoints = recomputed;
                    i = 0;
                }
            }
        }

        ClearMoveTarget(context);

        if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
        {
            progress?.Report("Đang đổi bản đồ, dừng lộ trình di chuyển.");
            return ActionResult.Success("Đang đổi bản đồ hoặc đã sang bản đồ mới, dừng lộ trình di chuyển.");
        }

        context.Network.CharMove();

        int finalX = context.Player.X;
        int finalY = context.Player.Y;
        progress?.Report($"Đã đến vị trí ({finalX}, {finalY}) thành công.");
        return ActionResult.Success($"Đã đến vị trí ({finalX}, {finalY}) thành công.");
    }

    private static async Task<bool> MoveToPointAsync(
        IGameContext context,
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
                ClearMoveTarget(context);
                return true;
            }

            SetNativeTarget(context, targetX, targetY);

            int initialY = context.Player.Y;
            if (targetY < initialY - 12)
            {
                context.Player.SetVelocityY(-10, 3);
            }

            await Task.Delay(40, cancellationToken);

            int anchorX = context.Player.X;
            int anchorY = context.Player.Y;
            var stuckSw = System.Diagnostics.Stopwatch.StartNew();
            var reportSw = System.Diagnostics.Stopwatch.StartNew();
            int unstuckAttempts = 0;

            while (!cancellationToken.IsCancellationRequested && context.IsRunning)
            {
                if (context.Player.Hp <= 0)
                    return false;

                if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
                {
                    ClearMoveTarget(context);
                    return true;
                }

                int curX = context.Player.X;
                int curY = context.Player.Y;

                if (reportSw.ElapsedMilliseconds >= 200)
                {
                    progress?.Report($"Vị trí: {curX}, {curY}");
                    reportSw.Restart();
                }

                int dist = Math.Abs(curX - targetX) + Math.Abs(curY - targetY);
                if (dist <= targetDist)
                {
                    progress?.Report($"Vị trí: {curX}, {curY}");
                    return true;
                }

                IntPtr cmp = context.Player.CurrentMovePoint;
                if (cmp == IntPtr.Zero)
                {
                    if (Math.Abs(curX - targetX) <= targetDist && Math.Abs(curY - targetY) <= targetDist)
                    {
                        progress?.Report($"Vị trí: {curX}, {curY}");
                        return true;
                    }

                    if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
                    {
                        ClearMoveTarget(context);
                        return true;
                    }

                    SetNativeTarget(context, targetX, targetY);
                    if (targetY < curY - 12)
                    {
                        context.Player.SetVelocityY(-10, 3);
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

                    await HandleUnstuckAsync(context, curX, curY, targetX, targetY, unstuckAttempts, tmw, tmh, types, progress, cancellationToken);
                    anchorX = context.Player.X;
                    anchorY = context.Player.Y;
                    stuckSw.Restart();
                }

                await Task.Delay(40, cancellationToken);
            }

            if (context.Player.IsChangingMap || (startMapId >= 0 && context.World.MapId != startMapId))
            {
                ClearMoveTarget(context);
                return true;
            }

            int finalX = context.Player.X;
            return Math.Abs(finalX - targetX) <= targetDist;
        }
        catch (OperationCanceledException)
        {
            ClearMoveTarget(context);
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static async Task HandleUnstuckAsync(
        IGameContext context,
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

            context.Player.SetVelocityY(-10, 3);
            SetNativeTarget(context, curX, escapeY);
        }
        else if (CanDrop(curX, curY, 48, tmw, tmh, types))
        {
            escapeY = curY + 48;
            progress?.Report($"Kẹt góc >2s tại ({curX}, {curY}), trần cản -> hạ thấp (Y={escapeY}) để thoát...");
            SetNativeTarget(context, curX, escapeY);
        }
        else
        {
            int stepBackX = curX + (targetX >= curX ? -32 : 32);
            progress?.Report($"Kẹt góc hẹp >2s tại ({curX}, {curY}), lùi nhẹ ({stepBackX}) lấy đà...");
            SetNativeTarget(context, stepBackX, curY);
            escapeY = curY;
        }

        var liftSw = System.Diagnostics.Stopwatch.StartNew();
        while (liftSw.ElapsedMilliseconds < 600 && !cancellationToken.IsCancellationRequested && context.IsRunning)
        {
            await Task.Delay(40, cancellationToken);
            int checkY = context.Player.Y;
            if (Math.Abs(checkY - escapeY) <= 20)
                break;
        }

        int nowX = context.Player.X;
        if (Math.Abs(nowX - targetX) > 24)
        {
            SetNativeTarget(context, targetX, escapeY);
        }
        else
        {
            SetNativeTarget(context, targetX, targetY);
        }
    }

    private static void ClearMoveTarget(IGameContext context)
    {
        context.Player.ClearMoveTarget();
        context.World.ClickMoving = false;
    }

    private static bool SetNativeTarget(IGameContext context, int targetX, int targetY)
    {
        try
        {
            if (context.Player.SetMoveTarget(targetX, targetY))
            {
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
