using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Algorithms;
using DragonBoyAccountManager.Core.Game.Schema;

namespace DragonBoyAccountManager.Core.Game.Actions;

/// <summary>
/// Hành động di chuyển tự động tới tọa độ (TargetX, TargetY).
/// Hỗ trợ 2 chế độ:
/// 1. FastTeleport: Dịch chuyển tức thời 3 bước đồng bộ Server (bỏ qua tìm đường).
/// 2. Native A* Navigation: Tìm đường A* trực giao 4 hướng tránh vật cản, kết hợp động cơ engine game và cơ chế chống kẹt góc.
/// </summary>
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

            int curX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
            int curY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);

            int effectiveTargetY = TargetY <= 0 ? context.World.GetYGround(TargetX) : TargetY;

            // Nếu đã ở rất gần mục tiêu (<= 15px)
            if (Math.Abs(curX - TargetX) <= 15 && Math.Abs(curY - effectiveTargetY) <= 15)
            {
                progress?.Report("Đã ở vị trí mục tiêu.");
                return ActionResult.Success("Đã ở vị trí mục tiêu.");
            }

            // 1. Chế độ Dịch chuyển nhanh (Teleport tức thời 3 bước đồng bộ Server chuẩn Xmap)
            if (IsFastTeleport)
            {
                if (context.Player.IsChangingMap)
                {
                    progress?.Report("Đang đổi map, không thể dịch chuyển nhanh.");
                    return ActionResult.Failure("Đang đổi map, không thể dịch chuyển nhanh.");
                }

                return await ExecuteFastTeleportAsync(context, myCharPtr, effectiveTargetY, progress, cancellationToken);
            }

            // 2. Chế độ Di chuyển thông minh A*
            return await ExecuteAStarMovementAsync(context, myCharPtr, curX, curY, effectiveTargetY, progress, cancellationToken);
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

    /// <summary>
    /// Thực thi dịch chuyển tức thời chuẩn Xmap (3 bước đồng bộ Server liên tiếp không delay).
    /// </summary>
    private async Task<ActionResult> ExecuteFastTeleportAsync(
        IGameContext context,
        IntPtr myCharPtr,
        int destY,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (context.Player.IsChangingMap)
        {
            progress?.Report("Đang đổi map, không thể dịch chuyển nhanh.");
            return ActionResult.Failure("Đang đổi map, không thể dịch chuyển nhanh.");
        }

        progress?.Report($"Dịch chuyển nhanh tới ({TargetX}, {destY})...");

        // 1. Dọn dẹp target di chuyển và triệt tiêu vận tốc dư thừa
        ClearMoveTarget(context, myCharPtr);

        // 2. Thuật toán Fast Teleport 3 bước đồng bộ Server chuẩn Xmap (không delay để tránh frame update làm trôi tọa độ)
        // Bước 1: Đặt vị trí tới (TargetX, destY) và gửi packet charMove
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, TargetX);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, destY);
        context.Network.CharMove();

        // Bước 2: Đặt vị trí đệm (TargetX, destY + 1) và gửi packet charMove
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, TargetX);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, destY + 1);
        context.Network.CharMove();

        // Bước 3: Chốt lại vị trí đích chính xác (TargetX, destY) và gửi packet charMove
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr, TargetX);
        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr, destY);
        context.Network.CharMove();

        // 3. Đảm bảo nhân vật dừng hẳn và xóa MovePoint
        ClearMoveTarget(context, myCharPtr);
        progress?.Report($"Pos: {TargetX}, {destY}");

        await Task.CompletedTask;
        return ActionResult.Success($"Đã dịch chuyển nhanh tới ({TargetX}, {destY}).");
    }

    /// <summary>
    /// Thực thi di chuyển theo lộ trình A* kết hợp Engine Game native.
    /// </summary>
    private async Task<ActionResult> ExecuteAStarMovementAsync(
        IGameContext context,
        IntPtr myCharPtr,
        int curX,
        int curY,
        int destY,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        // Đọc dữ liệu TileMap để nhận diện địa hình
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

        // Tính toán lộ trình A*
        List<(int X, int Y)> waypoints = [];
        if (types != null && tmw > 0 && tmh > 0)
        {
            progress?.Report($"Đang tính toán đường đi A* tới ({TargetX}, {destY})...");
            waypoints = TileAStarFinder.FindPath(curX, curY, TargetX, destY, tmw, tmh, types);
        }

        // Nếu không có lộ trình A*, fallback đi thẳng tới mục tiêu
        if (waypoints.Count <= 1)
        {
            waypoints = [(curX, curY), (TargetX, destY)];
        }

        progress?.Report($"Đi theo lộ trình ({waypoints.Count} mốc)...");

        // Di chuyển lần lượt qua các điểm mốc
        for (int i = 1; i < waypoints.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                return ActionResult.Failure("Bị hủy hoặc game đã tắt.");

            if (context.Player.IsChangingMap)
            {
                progress?.Report("Đang đổi map, dừng lộ trình di chuyển.");
                return ActionResult.Success("Đang đổi map, dừng lộ trình di chuyển.");
            }

            var wp = waypoints[i];
            bool isLast = (i == waypoints.Count - 1);
            int threshold = isLast ? 16 : 24;

            bool stepOk = await MoveToPointAsync(context, myCharPtr, wp.X, wp.Y, threshold, tmw, tmh, types, cancellationToken, progress);
            if (!stepOk && !isLast && types != null)
            {
                // Nếu kẹt giữa chừng, tính lại A* từ vị trí hiện tại
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

        // Dọn dẹp con trỏ mục tiêu và đồng bộ trạng thái cuối
        ClearMoveTarget(context, myCharPtr);
        context.Network.CharMove();

        int finalX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
        int finalY = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, myCharPtr);
        progress?.Report($"Đã đến vị trí ({finalX}, {finalY}) thành công.");
        return ActionResult.Success($"Đã đến vị trí ({finalX}, {finalY}) thành công.");
    }

    /// <summary>
    /// Điều khiển nhân vật di chuyển tới 1 điểm cụ thể bằng Native Engine và xử lý chống kẹt.
    /// </summary>
    private static async Task<bool> MoveToPointAsync(
        IGameContext context, 
        IntPtr myCharPtr, 
        int targetX, 
        int targetY, 
        int targetDist,
        int tmw,
        int tmh,
        int[]? types,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        try
        {
            SetNativeTarget(context, myCharPtr, targetX, targetY);

            // Nếu mục tiêu ở trên cao: kích hoạt xung lực nâng cvy = -10 và statusMe = 3 (Jump)
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

                    SetNativeTarget(context, myCharPtr, targetX, targetY);
                    if (targetY < curY - 12)
                    {
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, -10);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 3);
                    }
                }

                // Kiểm tra tiến độ di chuyển dựa trên điểm neo (Anchor).
                // Chỉ reset khi nhân vật đã thực sự tiến xa hơn 1 ô tile (> 24px)
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

            int finalX = context.Reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, myCharPtr);
            return Math.Abs(finalX - targetX) <= targetDist;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Xử lý thoát kẹt khi nhân vật đứng yên hoặc dao động nhỏ tại 1 vị trí quá 2 giây.
    /// </summary>
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

        // Chờ nhân vật dịch chuyển thoát góc kẹp
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
