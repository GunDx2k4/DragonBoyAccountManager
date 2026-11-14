using System.Buffers;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Navigation;

public class AutoNavigator(ICommandDispatcher dispatcher)
{
    private static readonly int[] Dx = [0, 0, 1, -1];
    private static readonly int[] Dy = [-1, 1, 0, 0];

    private readonly ICommandDispatcher _dispatcher = dispatcher;

    public async Task<Result> NavigateAsync(
        PlayerSnapshot player,
        MapSnapshot map,
        int targetX,
        int targetY,
        bool isFastTeleport = false,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!player.IsInGame || !player.IsAlive)
            return Result.Failure("Nhân vật chưa vào game hoặc đã tử vong.");

        bool preferGround = targetY <= 0;
        int effectiveY = preferGround ? map.GetYGround(targetX) : targetY;

        if (map.IsValid)
        {
            (targetX, effectiveY) = map.ResolveSafePosition(targetX, effectiveY, preferGround);
        }

        if (Math.Abs(player.X - targetX) <= 15 && Math.Abs(player.Y - effectiveY) <= 15)
        {
            progress?.Report("Đã ở vị trí mục tiêu.");
            return Result.Success("Đã ở vị trí mục tiêu.");
        }

        if (isFastTeleport)
        {
            progress?.Report($"Dịch chuyển tức thời tới ({targetX}, {effectiveY})...");
            return await _dispatcher.DispatchAsync(new MoveCommand(targetX, effectiveY, true), cancellationToken);
        }

        if (!map.IsValid)
        {
            return await _dispatcher.DispatchAsync(new MoveCommand(targetX, effectiveY, false), cancellationToken);
        }

        progress?.Report($"Đang tìm đường A* tới ({targetX}, {effectiveY})...");
        var path = FindPath(map, player.X, player.Y, targetX, effectiveY);

        if (path.Count <= 1)
        {
            return await _dispatcher.DispatchAsync(new MoveCommand(targetX, effectiveY, false), cancellationToken);
        }

        progress?.Report($"Đi theo lộ trình {path.Count} mốc...");
        int prevX = player.X;
        int prevY = player.Y;
        for (int i = 1; i < path.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return Result.Failure("Quá trình di chuyển bị hủy.");

            var (wx, wy) = path[i];
            var stepResult = await _dispatcher.DispatchAsync(new MoveCommand(wx, wy, false), cancellationToken);
            if (stepResult.IsFailure)
                return stepResult;

            double stepDist = Math.Sqrt((double)(wx - prevX) * (wx - prevX) + (double)(wy - prevY) * (wy - prevY));
            int waitMs = Math.Clamp((int)(stepDist * 3.5), 120, 800);
            prevX = wx;
            prevY = wy;
            await Task.Delay(waitMs, cancellationToken);
        }

        return Result.Success($"Đã tới ({targetX}, {effectiveY})");
    }

    private static List<(int X, int Y)> FindPath(MapSnapshot map, int startPx, int startPy, int targetPx, int targetPy)
    {
        var result = new List<(int X, int Y)>();
        if (!map.IsValid)
            return result;

        int tmw = map.Width;
        int tmh = map.Height;

        var (startTx, startTy) = PixelToCell(map, startPx, startPy);
        var (targetTx, targetTy) = PixelToCell(map, targetPx, targetPy);

        if (!map.IsOpenSpace(startTx, startTy))
        {
            var nearestStart = FindNearestOpenSpace(map, startTx, startTy, 6);
            if (nearestStart.HasValue)
            {
                (startTx, startTy) = nearestStart.Value;
            }
        }

        if (!map.IsOpenSpace(targetTx, targetTy))
        {
            var nearestTarget = FindNearestOpenSpace(map, targetTx, targetTy, 6);
            if (!nearestTarget.HasValue)
                return result;

            (targetTx, targetTy) = nearestTarget.Value;
        }

        if (startTx == targetTx && startTy == targetTy)
        {
            result.Add((targetPx, targetPy));
            return result;
        }

        int totalNodes = tmw * tmh;
        int startIndex = startTy * tmw + startTx;
        int targetIndex = targetTy * tmw + targetTx;

        float[] gScore = ArrayPool<float>.Shared.Rent(totalNodes);
        int[] parent = ArrayPool<int>.Shared.Rent(totalNodes);
        try
        {
            Array.Fill(gScore, float.MaxValue, 0, totalNodes);
            Array.Fill(parent, -1, 0, totalNodes);

            var openSet = new PriorityQueue<int, float>();
            gScore[startIndex] = 0;
            openSet.Enqueue(startIndex, Heuristic(startTx, startTy, targetTx, targetTy));

            bool found = false;
            while (openSet.Count > 0)
            {
                int current = openSet.Dequeue();
                if (current == targetIndex)
                {
                    found = true;
                    break;
                }

                int curX = current % tmw;
                int curY = current / tmw;
                float currentG = gScore[current];
                bool curHasGround = map.IsGroundBelow(curX, curY);

                int parentIdx = parent[current];
                int prevDirX = parentIdx != -1 ? curX - (parentIdx % tmw) : 0;
                int prevDirY = parentIdx != -1 ? curY - (parentIdx / tmw) : 0;

                for (int i = 0; i < 4; i++)
                {
                    int nx = curX + Dx[i];
                    int ny = curY + Dy[i];

                    if (nx < 0 || nx >= tmw || ny < 1 || ny >= tmh || !map.IsOpenSpace(nx, ny))
                        continue;

                    bool neighborHasGround = map.IsGroundBelow(nx, ny);
                    float stepCost = (curHasGround && neighborHasGround && ny == curY) ? 1.0f
                        : (Dx[i] == 0 && Dy[i] == -1) ? 1.3f
                        : (Dx[i] == 0 && Dy[i] == 1) ? 1.2f
                        : 1.8f;

                    if ((prevDirX != 0 || prevDirY != 0) && (Dx[i] != prevDirX || Dy[i] != prevDirY))
                    {
                        stepCost += 0.15f;
                    }

                    int neighborIndex = ny * tmw + nx;
                    float tentativeG = currentG + stepCost;

                    if (tentativeG < gScore[neighborIndex])
                    {
                        parent[neighborIndex] = current;
                        gScore[neighborIndex] = tentativeG;
                        openSet.Enqueue(neighborIndex, tentativeG + Heuristic(nx, ny, targetTx, targetTy));
                    }
                }
            }

            if (!found)
                return result;

            var cellPath = new List<(int X, int Y)>();
            int currIdx = targetIndex;
            while (currIdx != -1)
            {
                cellPath.Add((currIdx % tmw, currIdx / tmw));
                currIdx = parent[currIdx];
            }
            cellPath.Reverse();

            var waypoints = CompressCollinearPath(cellPath);
            result.Add((startPx, startPy));
            for (int i = 1; i < waypoints.Count - 1; i++)
            {
                var (tx, ty) = waypoints[i];
                result.Add((tx * MapSnapshot.TileSize + MapSnapshot.TileSize / 2, (ty + 1) * MapSnapshot.TileSize));
            }
            result.Add((targetPx, targetPy));
            return result;
        }
        finally
        {
            ArrayPool<float>.Shared.Return(gScore);
            ArrayPool<int>.Shared.Return(parent);
        }
    }

    private static List<(int X, int Y)> CompressCollinearPath(List<(int X, int Y)> cellPath)
    {
        if (cellPath.Count <= 2)
            return cellPath;

        var compressed = new List<(int X, int Y)> { cellPath[0] };
        for (int i = 1; i < cellPath.Count - 1; i++)
        {
            var prev = cellPath[i - 1];
            var curr = cellPath[i];
            var next = cellPath[i + 1];

            if (curr.X - prev.X != next.X - curr.X || curr.Y - prev.Y != next.Y - curr.Y)
            {
                compressed.Add(curr);
            }
        }

        compressed.Add(cellPath[^1]);
        return compressed;
    }

    private static (int X, int Y) PixelToCell(MapSnapshot map, int px, int py)
    {
        int tx = Math.Clamp(px / MapSnapshot.TileSize, 0, map.Width - 1);
        int rawTy = Math.Clamp((int)Math.Round((double)py / MapSnapshot.TileSize), 0, map.Height - 1);

        if (rawTy > 0 && map.IsWall(tx, rawTy) && !map.IsWall(tx, rawTy - 1))
        {
            return (tx, rawTy - 1);
        }

        int airTy = Math.Clamp(py / MapSnapshot.TileSize, 0, map.Height - 1);
        if (map.IsOpenSpace(tx, airTy))
        {
            return (tx, airTy);
        }

        return (tx, rawTy);
    }

    private static (int X, int Y)? FindNearestOpenSpace(MapSnapshot map, int cx, int cy, int radius)
    {
        for (int r = 0; r <= radius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (map.IsOpenSpace(nx, ny) && map.IsGroundBelow(nx, ny))
                        return (nx, ny);
                }
            }
        }

        for (int r = 1; r <= radius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (map.IsOpenSpace(nx, ny))
                        return (nx, ny);
                }
            }
        }

        return null;
    }

    private static float Heuristic(int x1, int y1, int x2, int y2) =>
        Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
}
