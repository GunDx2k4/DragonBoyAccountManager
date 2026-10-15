using DragonBoyAccountManager.Core.Game.Pathfinding;

namespace DragonBoyAccountManager.Core.Game.Actions;

public class AutoMoveAction(int targetX, int targetY, bool isFly = false) : IGameAction
{
    public string Name => $"Auto Move to ({targetX}, {targetY})";

    public int TargetX => targetX;
    public int TargetY => targetY;
    public bool IsFly => isFly;

    public async Task<bool> ExecuteAsync(
        GameInstance instance, 
        IProgress<string>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!instance.IsRunning || !instance.MyChar.IsInGame)
            {
                progress?.Report("Game chưa sẵn sàng hoặc chưa đăng nhập.");
                return false;
            }

            int curX = instance.MyChar.X;
            int curY = instance.MyChar.Y;

            // Nếu đã ở rất gần mục tiêu (<= 15px)
            if (Math.Abs(curX - TargetX) <= 15 && Math.Abs(curY - TargetY) <= 15)
            {
                progress?.Report("Đã ở vị trí mục tiêu.");
                return true;
            }

            IntPtr myCharPtr = instance.Reflector.ReadStatic<IntPtr>("Char", "myChar");
            if (myCharPtr == IntPtr.Zero)
            {
                progress?.Report("Không tìm thấy con trỏ nhân vật myChar.");
                return false;
            }

            // Đọc dữ liệu TileMap để nhận diện địa hình
            (int Width, int Height, int[] Types)? mapData = null;
            for (int i = 0; i < 3; i++)
            {
                mapData = instance.Map.GetTileMapCollisionData();
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
                bool ok = await MoveToPointAsync(instance, myCharPtr, TargetX, TargetY, 16, cancellationToken);
                SendCharMove(instance);
                return ok;
            }

            // Nếu A* trả về 1 đoạn thẳng duy nhất không có vật cản
            if (waypoints.Count == 2)
            {
                progress?.Report($"Đường đi thẳng tới ({TargetX}, {TargetY})...");
                bool ok = await MoveToPointAsync(instance, myCharPtr, TargetX, TargetY, 16, cancellationToken);
                SendCharMove(instance);
                return ok;
            }

            // 2. Di chuyển lần lượt qua các điểm mốc A* bằng Native Game Engine
            progress?.Report($"Đi theo lộ trình ({waypoints.Count} mốc) chuẩn engine game...");
            for (int i = 1; i < waypoints.Count; i++)
            {
                if (cancellationToken.IsCancellationRequested || !instance.IsRunning)
                    return false;

                var wp = waypoints[i];
                bool isLast = (i == waypoints.Count - 1);
                int threshold = isLast ? 16 : 24;

                bool stepOk = await MoveToPointAsync(instance, myCharPtr, wp.X, wp.Y, threshold, cancellationToken);
                if (!stepOk && !isLast)
                {
                    // Nếu kẹt giữa chừng, tính lại A* từ vị trí hiện tại
                    int nowX = instance.MyChar.X;
                    int nowY = instance.MyChar.Y;
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
            instance.Reflector.WriteField("Char", "currentMovePoint", myCharPtr, IntPtr.Zero);
            SendCharMove(instance);

            progress?.Report($"Đã đến vị trí ({instance.MyChar.X}, {instance.MyChar.Y}) thành công.");
            return true;
        }
        catch (OperationCanceledException)
        {
            progress?.Report("Quá trình di chuyển bị hủy bỏ.");
            return false;
        }
        catch (Exception ex)
        {
            progress?.Report($"Lỗi khi di chuyển: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Giao điểm đích cho engine game di chuyển tự nhiên qua currentMovePoint (như mod pk9r)
    /// hoặc qua GameScr.checkClickMoveTo.
    /// </summary>
    private static bool SetNativeTarget(GameInstance instance, IntPtr myCharPtr, int targetX, int targetY)
    {
        try
        {
            // 1. Thử tạo MovePoint trực tiếp như pk9r
            IntPtr mp = instance.Reflector.CreateInstance("MovePoint", targetX, targetY);
            if (mp != IntPtr.Zero)
            {
                int curX = instance.MyChar.X;
                instance.Reflector.WriteField("MovePoint", "xEnd", mp, targetX);
                instance.Reflector.WriteField("MovePoint", "yEnd", mp, targetY);
                instance.Reflector.WriteField("Char", "currentMovePoint", myCharPtr, mp);
                instance.Reflector.WriteField("Char", "cdir", myCharPtr, targetX >= curX ? 1 : -1);
                instance.Reflector.WriteField("Char", "endMovePointCommand", myCharPtr, IntPtr.Zero);

                IntPtr gameScr = instance.Reflector.CallMethod("GameScr", "gI", IntPtr.Zero);
                if (gameScr == IntPtr.Zero)
                    gameScr = instance.Reflector.ReadStatic<IntPtr>("GameScr", "instance");
                if (gameScr != IntPtr.Zero)
                    instance.Reflector.WriteField("GameScr", "clickMoving", gameScr, true);

                return true;
            }

            // 2. Dự phòng: gọi checkClickMoveTo của GameScr
            IntPtr gs = instance.Reflector.CallMethod("GameScr", "gI", IntPtr.Zero);
            if (gs == IntPtr.Zero)
                gs = instance.Reflector.ReadStatic<IntPtr>("GameScr", "instance");

            if (gs != IntPtr.Zero)
            {
                instance.Reflector.CallMethod("GameScr", "checkClickMoveTo", gs, targetX, targetY, 0);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Cho engine game tự lái nhân vật đến điểm mốc (targetX, targetY).
    /// Game engine tự tính toán va chạm, gia tốc, animation và tự gửi Service.charMove() chuẩn 100% server.
    /// </summary>
    private static async Task<bool> MoveToPointAsync(
        GameInstance instance, 
        IntPtr myCharPtr, 
        int targetX, 
        int targetY, 
        int targetDist,
        CancellationToken cancellationToken)
    {
        try
        {
            SetNativeTarget(instance, myCharPtr, targetX, targetY);
            await Task.Delay(40, cancellationToken);

            int prevX = instance.MyChar.X;
            int prevY = instance.MyChar.Y;
            int stuckTicks = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested && instance.IsRunning)
            {
                int curX = instance.MyChar.X;
                int curY = instance.MyChar.Y;

                // Nếu đã tới trong bán kính cho phép
                int dist = Math.Abs(curX - targetX) + Math.Abs(curY - targetY);
                if (dist <= targetDist)
                {
                    return true;
                }

                // Nếu game engine đã hoàn thành chặng (currentMovePoint tự clear về null khi chạm đích)
                IntPtr cmp = instance.Reflector.ReadFieldPtr("Char", "currentMovePoint", myCharPtr);
                if (cmp == IntPtr.Zero)
                {
                    if (Math.Abs(curX - targetX) <= 24)
                        return true;

                    // Nếu game mất currentMovePoint nhưng chưa tới nơi, gán lại
                    SetNativeTarget(instance, myCharPtr, targetX, targetY);
                }

                // Kiểm tra đứng yên (stuck)
                if (Math.Abs(curX - prevX) <= 1 && Math.Abs(curY - prevY) <= 1)
                {
                    stuckTicks++;
                    if (stuckTicks > 25) // Đứng yên ~1s
                    {
                        // Gán lại mốc
                        SetNativeTarget(instance, myCharPtr, targetX, targetY);
                        stuckTicks = 0;

                        // Nếu kẹt quá 3.5 giây ở điểm này
                        if (sw.ElapsedMilliseconds > 3500)
                        {
                            return false;
                        }
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

            return Math.Abs(instance.MyChar.X - targetX) <= targetDist;
        }
        catch
        {
            return false;
        }
    }

    public static void SendCharMove(GameInstance instance)
    {
        try
        {
            IntPtr service = instance.Reflector.CallMethod("Service", "gI", IntPtr.Zero);
            if (service == IntPtr.Zero)
            {
                service = instance.Reflector.ReadStatic<IntPtr>("Service", "instance");
            }

            if (service != IntPtr.Zero)
            {
                instance.Reflector.CallMethod("Service", "charMove", service);
            }
        }
        catch { }
    }
}
