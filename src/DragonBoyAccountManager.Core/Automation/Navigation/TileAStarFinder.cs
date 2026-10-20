namespace DragonBoyAccountManager.Core.Automation.Navigation;

public static class TileAStarFinder
{
    public const int TileSize = 24;

    public const int T_EMPTY = 0;
    public const int T_TOP = 2;
    public const int T_LEFT = 4;
    public const int T_RIGHT = 8;
    public const int T_SOLIDGROUND = 4096;
    public const int T_BOTTOM = 8192;
    public const int T_DIE = 16384;
    public const int T_HEBI = 32768;
    public const int T_BANG = 65536;
    public const int T_CENTER = 1;
    public const int T_BRIDGE = 1024;

    public const int WallMask = T_SOLIDGROUND | T_DIE | T_HEBI | T_BANG |
                                T_TOP | T_LEFT | T_RIGHT | T_BOTTOM |
                                T_CENTER | T_BRIDGE;

    private static readonly int[] Dx = [0, 0, 1, -1];
    private static readonly int[] Dy = [-1, 1, 0, 0];

    public static List<(int X, int Y)> FindPath(
        int startPx, int startPy,
        int targetPx, int targetPy,
        int tmw, int tmh, int[] types)
    {
        var result = new List<(int X, int Y)>();
        if (tmw <= 0 || tmh <= 0 || types == null || types.Length < tmw * tmh)
            return result;

        var (startTx, startTy) = PixelToCell(startPx, startPy, tmw, tmh, types);
        var (targetTx, targetTy) = PixelToCell(targetPx, targetPy, tmw, tmh, types);

        if (!IsOpenSpace(startTx, startTy, tmw, tmh, types))
        {
            var nearestStart = FindNearestOpenSpace(startTx, startTy, tmw, tmh, types, 6);
            if (nearestStart.HasValue)
            {
                startTx = nearestStart.Value.X;
                startTy = nearestStart.Value.Y;
            }
        }

        if (!IsOpenSpace(targetTx, targetTy, tmw, tmh, types))
        {
            var nearestTarget = FindNearestOpenSpace(targetTx, targetTy, tmw, tmh, types, 6);
            if (nearestTarget.HasValue)
            {
                targetTx = nearestTarget.Value.X;
                targetTy = nearestTarget.Value.Y;
            }
            else
            {
                return result;
            }
        }

        if (startTx == targetTx && startTy == targetTy)
        {
            result.Add((targetPx, targetPy));
            return result;
        }

        int totalNodes = tmw * tmh;
        int startIndex = startTy * tmw + startTx;
        int targetIndex = targetTy * tmw + targetTx;

        float[] gScore = System.Buffers.ArrayPool<float>.Shared.Rent(totalNodes);
        int[] parent = System.Buffers.ArrayPool<int>.Shared.Rent(totalNodes);
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
            bool curHasGround = IsGroundBelow(curX, curY, tmw, tmh, types);

            int parentIdx = parent[current];
            int prevDirX = parentIdx != -1 ? curX - (parentIdx % tmw) : 0;
            int prevDirY = parentIdx != -1 ? curY - (parentIdx / tmw) : 0;

            for (int i = 0; i < 4; i++)
            {
                int nx = curX + Dx[i];
                int ny = curY + Dy[i];

                if (nx < 0 || nx >= tmw || ny < 1 || ny >= tmh)
                    continue;

                if (!IsOpenSpace(nx, ny, tmw, tmh, types))
                    continue;

                bool neighborHasGround = IsGroundBelow(nx, ny, tmw, tmh, types);
                float stepCost;

                if (curHasGround && neighborHasGround && ny == curY)
                {
                    stepCost = 1.0f;
                }
                else if (Dx[i] == 0 && Dy[i] == -1)
                {
                    stepCost = 1.3f;
                }
                else if (Dx[i] == 0 && Dy[i] == 1)
                {
                    stepCost = 1.2f;
                }
                else
                {
                    stepCost = 1.8f;
                }

                if (prevDirX != 0 || prevDirY != 0)
                {
                    if (Dx[i] != prevDirX || Dy[i] != prevDirY)
                    {
                        stepCost += 0.15f;
                    }
                }

                int neighborIndex = ny * tmw + nx;
                float tentativeG = currentG + stepCost;

                if (tentativeG < gScore[neighborIndex])
                {
                    parent[neighborIndex] = current;
                    gScore[neighborIndex] = tentativeG;
                    float fScore = tentativeG + Heuristic(nx, ny, targetTx, targetTy);
                    openSet.Enqueue(neighborIndex, fScore);
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
            var (px, py) = CellToFootPixel(tx, ty);
            result.Add((px, py));
        }
        result.Add((targetPx, targetPy));

        return result;
        }
        finally
        {
            System.Buffers.ArrayPool<float>.Shared.Return(gScore);
            System.Buffers.ArrayPool<int>.Shared.Return(parent);
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

            int dx1 = curr.X - prev.X;
            int dy1 = curr.Y - prev.Y;
            int dx2 = next.X - curr.X;
            int dy2 = next.Y - curr.Y;

            if (dx1 != dx2 || dy1 != dy2)
            {
                compressed.Add(curr);
            }
        }

        compressed.Add(cellPath[^1]);
        return compressed;
    }

    public static bool IsWall(int tx, int ty, int tmw, int tmh, int[] types)
    {
        if (tx < 0 || tx >= tmw || ty < 0 || ty >= tmh)
            return true;

        int index = ty * tmw + tx;
        if (index < 0 || index >= types.Length)
            return true;

        return (types[index] & WallMask) != 0;
    }

    public static bool IsOpenSpace(int tx, int ty, int tmw, int tmh, int[] types)
    {
        if (IsWall(tx, ty, tmw, tmh, types)) return false;
        if (ty - 1 >= 0 && IsWall(tx, ty - 1, tmw, tmh, types)) return false;
        return true;
    }

    public static bool IsGroundBelow(int tx, int ty, int tmw, int tmh, int[] types)
    {
        if (ty + 1 >= tmh) return false;
        int belowIndex = (ty + 1) * tmw + tx;
        if (belowIndex < 0 || belowIndex >= types.Length) return false;

        int belowType = types[belowIndex];
        return (belowType & WallMask) != 0 && (belowType & T_DIE) == 0;
    }

    public static (int X, int Y) CellToFootPixel(int tx, int ty)
    {
        int px = tx * TileSize + (TileSize / 2);
        int py = (ty + 1) * TileSize;
        return (px, py);
    }

    public static (int X, int Y) PixelToCell(int px, int py, int tmw, int tmh, int[] types)
    {
        int tx = Math.Clamp(px / TileSize, 0, tmw - 1);
        int rawTy = Math.Clamp((int)Math.Round((double)py / TileSize), 0, tmh - 1);

        if (rawTy > 0 && IsWall(tx, rawTy, tmw, tmh, types) && !IsWall(tx, rawTy - 1, tmw, tmh, types))
        {
            return (tx, rawTy - 1);
        }

        int airTy = Math.Clamp(py / TileSize, 0, tmh - 1);
        if (IsOpenSpace(tx, airTy, tmw, tmh, types))
        {
            return (tx, airTy);
        }

        return (tx, Math.Clamp(rawTy, 0, tmh - 1));
    }

    public static (int X, int Y)? FindNearestOpenSpace(int cx, int cy, int tmw, int tmh, int[] types, int radius)
    {
        for (int r = 0; r <= radius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (IsOpenSpace(nx, ny, tmw, tmh, types) && IsGroundBelow(nx, ny, tmw, tmh, types))
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
                    if (IsOpenSpace(nx, ny, tmw, tmh, types))
                        return (nx, ny);
                }
            }
        }

        return null;
    }

    private static float Heuristic(int x1, int y1, int x2, int y2)
    {
        return Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }
}
