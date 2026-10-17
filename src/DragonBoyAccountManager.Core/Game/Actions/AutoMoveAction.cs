using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Algorithms;
using DragonBoyAccountManager.Core.Game.Schema;

namespace DragonBoyAccountManager.Core.Game.Actions;

public class AutoMoveAction(int targetX, int targetY, bool isFly = false) 
    : GameAction($"Auto Move to ({targetX}, {targetY})")
{
    public int TargetX => targetX;
    public int TargetY => targetY;
    public bool IsFly => isFly;

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

            int curX = context.Player.X;
            int curY = context.Player.Y;

            // Nếu đã ở rất gần mục tiêu (<= 15px)
            if (Math.Abs(curX - TargetX) <= 15 && Math.Abs(curY - TargetY) <= 15)
            {
                progress?.Report("Đã ở vị trí mục tiêu.");
                return ActionResult.Success("Đã ở vị trí mục tiêu.");
            }

            IntPtr myCharPtr = context.Player.Pointer;
            if (myCharPtr == IntPtr.Zero)
            {
                progress?.Report("Không tìm thấy con trỏ nhân vật myChar.");
                return ActionResult.Failure("Không tìm thấy con trỏ nhân vật myChar.");
            }

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

            // 1. Tính toán lộ trình A* nếu không bật chế độ bay
            List<(int X, int Y)> waypoints = [];
            if (!IsFly && types != null && tmw > 0 && tmh > 0)
            {
                progress?.Report($"Đang tính toán đường đi A* tới ({TargetX}, {TargetY})...");
                waypoints = TileAStarFinder.FindPath(curX, curY, TargetX, TargetY, tmw, tmh, types);
            }
            else if (IsFly && Math.Abs(curX - TargetX) > 30)
            {
                // Ở chế độ bay, nâng độ cao bay lên khỏi mặt đất để tránh va quẹt góc/gờ đất
                int cruiseLift = 64;
                if (CanFlyUp(curX, curY, cruiseLift, tmw, tmh, types))
                {
                    int cruiseY = Math.Max(24, Math.Min(curY, TargetY) - cruiseLift);
                    waypoints = [(curX, curY), (curX, cruiseY), (TargetX, cruiseY), (TargetX, TargetY)];
                }
            }

            // Nếu không có lộ trình A* hoặc là đường bay thẳng ngắn
            if (waypoints.Count <= 1)
            {
                progress?.Report($"Di chuyển tới ({TargetX}, {TargetY})...");
                bool ok = await MoveToPointAsync(context, myCharPtr, TargetX, TargetY, 16, tmw, tmh, types, IsFly, cancellationToken, progress);
                context.Network.CharMove();
                return ok ? ActionResult.Success() : ActionResult.Failure($"Không thể di chuyển tới ({TargetX}, {TargetY}).");
            }

            // Nếu chỉ có 2 mốc
            if (waypoints.Count == 2)
            {
                progress?.Report($"Đường đi thẳng tới ({TargetX}, {TargetY})...");
                bool ok = await MoveToPointAsync(context, myCharPtr, TargetX, TargetY, 16, tmw, tmh, types, IsFly, cancellationToken, progress);
                context.Network.CharMove();
                return ok ? ActionResult.Success() : ActionResult.Failure($"Không thể di chuyển tới ({TargetX}, {TargetY}).");
            }

            // 2. Di chuyển lần lượt qua các điểm mốc bằng Native Game Engine
            progress?.Report($"Đi theo lộ trình ({waypoints.Count} mốc) chuẩn engine game...");
            for (int i = 1; i < waypoints.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                    return ActionResult.Failure("Bị hủy hoặc game đã tắt.");

                var wp = waypoints[i];
                bool isLast = (i == waypoints.Count - 1);
                int threshold = isLast ? 16 : 24;

                bool stepOk = await MoveToPointAsync(context, myCharPtr, wp.X, wp.Y, threshold, tmw, tmh, types, IsFly, cancellationToken, progress);
                if (!stepOk && !isLast)
                {
                    if (!IsFly && types != null)
                    {
                        // Nếu kẹt giữa chừng (và không phải chế độ bay), tính lại A* từ vị trí hiện tại
                        int nowX = context.Player.X;
                        int nowY = context.Player.Y;
                        var recomputed = TileAStarFinder.FindPath(nowX, nowY, TargetX, TargetY, tmw, tmh, types);
                        if (recomputed.Count > 1)
                        {
                            waypoints = recomputed;
                            i = 0;
                            continue;
                        }
                    }
                }
            }

            // Dọn dẹp con trỏ mục tiêu và đồng bộ trạng thái cuối
            context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, myCharPtr, IntPtr.Zero);
            context.Network.CharMove();

            progress?.Report($"Đã đến vị trí ({context.Player.X}, {context.Player.Y}) thành công.");
            return ActionResult.Success($"Đã đến vị trí ({context.Player.X}, {context.Player.Y}) thành công.");
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

    private static bool SetNativeTarget(IGameContext context, IntPtr myCharPtr, int targetX, int targetY)
    {
        try
        {
            IntPtr mp = context.Reflector.CreateInstance(GameSchema.MovePoint.ClassName, targetX, targetY);
            if (mp != IntPtr.Zero)
            {
                int curX = context.Player.X;
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

    private static async Task<bool> MoveToPointAsync(
        IGameContext context, 
        IntPtr myCharPtr, 
        int targetX, 
        int targetY, 
        int targetDist,
        int tmw,
        int tmh,
        int[]? types,
        bool isFly,
        CancellationToken cancellationToken,
        IProgress<string>? progress = null)
    {
        try
        {
            SetNativeTarget(context, myCharPtr, targetX, targetY);

            // Nếu mục tiêu ở trên cao hoặc đang bật chế độ bay:
            // Kích hoạt xung lực nâng cvy = -10 và statusMe = 3 (Jump) để lập tức tách khỏi mặt đất
            // (TileMap.tileTypeAt(cx, cy, 2) == false), mở khóa động cơ bay native của game.
            int initialY = context.Player.Y;
            if (targetY < initialY - 12 || (isFly && Math.Abs(context.Player.X - targetX) > 20))
            {
                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, -10);
                context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 3);
            }

            await Task.Delay(40, cancellationToken);

            int anchorX = context.Player.X;
            int anchorY = context.Player.Y;
            var stuckSw = System.Diagnostics.Stopwatch.StartNew();
            int unstuckAttempts = 0;

            while (!cancellationToken.IsCancellationRequested && context.IsRunning)
            {
                if (context.Player.Hp <= 0)
                    return false;

                int curX = context.Player.X;
                int curY = context.Player.Y;

                int dist = Math.Abs(curX - targetX) + Math.Abs(curY - targetY);
                if (dist <= targetDist)
                    return true;

                IntPtr cmp = context.Reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, myCharPtr);
                if (cmp == IntPtr.Zero)
                {
                    if (Math.Abs(curX - targetX) <= targetDist && Math.Abs(curY - targetY) <= targetDist)
                        return true;

                    SetNativeTarget(context, myCharPtr, targetX, targetY);
                    if (targetY < curY - 12 || (isFly && Math.Abs(curX - targetX) > 20))
                    {
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, myCharPtr, -10);
                        context.Reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, myCharPtr, 3);
                    }
                }

                // Kiểm tra tiến độ di chuyển dựa trên điểm neo (Anchor).
                // Chỉ reset đồng hồ kẹt khi nhân vật đã thực sự tiến xa hơn 1 ô tile (> 24px)
                // để tránh dao động rung lắc 2-3px tại góc kẹt làm reset oan đồng hồ 2s.
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
                    {
                        return false;
                    }

                    int liftDistance = 64 + (unstuckAttempts * 24); // 64px, 88px, 112px, 136px
                    bool canFlyUp = CanFlyUp(curX, curY, liftDistance, tmw, tmh, types);
                    int escapeY;

                    if (canFlyUp)
                    {
                        escapeY = Math.Max(24, curY - liftDistance);
                        progress?.Report($"Kẹt góc >2s tại ({curX}, {curY}), bay vút lên cao (Y={escapeY}) để thoát kẹp...");

                        // Bứt phá lực nâng: cvy = -10 và statusMe = 3 (Jump) để tách khỏi góc cản
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

                    // Chờ nhân vật nhấc bổng / dịch chuyển thoát góc kẹp
                    var liftSw = System.Diagnostics.Stopwatch.StartNew();
                    while (liftSw.ElapsedMilliseconds < 600 && !cancellationToken.IsCancellationRequested && context.IsRunning)
                    {
                        await Task.Delay(40, cancellationToken);
                        if (Math.Abs(context.Player.Y - escapeY) <= 20)
                            break;
                    }

                    // Sau khi nhấc bổng, bay ngang tới targetX ở độ cao thoát hiểm nếu còn xa
                    if (Math.Abs(context.Player.X - targetX) > 24)
                    {
                        SetNativeTarget(context, myCharPtr, targetX, escapeY);
                    }
                    else
                    {
                        SetNativeTarget(context, myCharPtr, targetX, targetY);
                    }

                    anchorX = context.Player.X;
                    anchorY = context.Player.Y;
                    stuckSw.Restart();
                }

                await Task.Delay(40, cancellationToken);
            }

            return Math.Abs(context.Player.X - targetX) <= targetDist;
        }
        catch
        {
            return false;
        }
    }
}
