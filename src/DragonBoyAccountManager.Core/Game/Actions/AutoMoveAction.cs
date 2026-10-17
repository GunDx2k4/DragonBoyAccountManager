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

            // Nếu không có lộ trình A* hoặc là đường bay thẳng
            if (waypoints.Count <= 1)
            {
                progress?.Report($"Di chuyển tới ({TargetX}, {TargetY})...");
                bool ok = await MoveToPointAsync(context, myCharPtr, TargetX, TargetY, 16, cancellationToken);
                context.Network.CharMove();
                return ok ? ActionResult.Success() : ActionResult.Failure($"Không thể di chuyển tới ({TargetX}, {TargetY}).");
            }

            // Nếu A* trả về 1 đoạn thẳng duy nhất không có vật cản
            if (waypoints.Count == 2)
            {
                progress?.Report($"Đường đi thẳng tới ({TargetX}, {TargetY})...");
                bool ok = await MoveToPointAsync(context, myCharPtr, TargetX, TargetY, 16, cancellationToken);
                context.Network.CharMove();
                return ok ? ActionResult.Success() : ActionResult.Failure($"Không thể di chuyển tới ({TargetX}, {TargetY}).");
            }

            // 2. Di chuyển lần lượt qua các điểm mốc A* bằng Native Game Engine
            progress?.Report($"Đi theo lộ trình ({waypoints.Count} mốc) chuẩn engine game...");
            for (int i = 1; i < waypoints.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                    return ActionResult.Failure("Bị hủy hoặc game đã tắt.");

                var wp = waypoints[i];
                bool isLast = (i == waypoints.Count - 1);
                int threshold = isLast ? 16 : 24;

                bool stepOk = await MoveToPointAsync(context, myCharPtr, wp.X, wp.Y, threshold, cancellationToken);
                if (!stepOk && !isLast)
                {
                    // Nếu kẹt giữa chừng, tính lại A* từ vị trí hiện tại
                    int nowX = context.Player.X;
                    int nowY = context.Player.Y;
                    var recomputed = TileAStarFinder.FindPath(nowX, nowY, TargetX, TargetY, tmw, tmh, types!);
                    if (recomputed.Count > 1)
                    {
                        waypoints = recomputed;
                        i = 0;
                        continue;
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

    private static async Task<bool> MoveToPointAsync(
        IGameContext context, 
        IntPtr myCharPtr, 
        int targetX, 
        int targetY, 
        int targetDist,
        CancellationToken cancellationToken)
    {
        try
        {
            SetNativeTarget(context, myCharPtr, targetX, targetY);
            await Task.Delay(40, cancellationToken);

            int prevX = context.Player.X;
            int prevY = context.Player.Y;
            int stuckTicks = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested && context.IsRunning)
            {
                int curX = context.Player.X;
                int curY = context.Player.Y;

                int dist = Math.Abs(curX - targetX) + Math.Abs(curY - targetY);
                if (dist <= targetDist)
                    return true;

                IntPtr cmp = context.Reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, myCharPtr);
                if (cmp == IntPtr.Zero)
                {
                    if (Math.Abs(curX - targetX) <= 24)
                        return true;

                    SetNativeTarget(context, myCharPtr, targetX, targetY);
                }

                if (Math.Abs(curX - prevX) <= 1 && Math.Abs(curY - prevY) <= 1)
                {
                    stuckTicks++;
                    if (stuckTicks > 25)
                    {
                        SetNativeTarget(context, myCharPtr, targetX, targetY);
                        stuckTicks = 0;

                        if (sw.ElapsedMilliseconds > 3500)
                            return false;
                    }
                }
                else
                {
                    stuckTicks = 0;
                    prevX = curX;
                    prevY = curY;
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
