namespace DragonBoyAccountManager.Core.Telemetry.Snapshots;

public sealed record MapSnapshot(
    int MapId,
    int ZoneId,
    string MapName,
    int Width,
    int Height,
    int[] Types
)
{
    public const int TileSize = 24;
    private const int WalkableMask = 2 | 1024;
    private const int SolidBodyMask = 4 | 8 | 4096 | 8192 | 16384 | 32768 | 65536;
    private const int WallMask = WalkableMask | SolidBodyMask | 1;

    public static MapSnapshot Empty => new(-1, -1, string.Empty, 0, 0, []);

    public bool IsValid => MapId >= 0 && Width > 0 && Height > 0 && Types.Length >= Width * Height;

    public int GetTileTypeAt(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return 1000;
        return Types[tileY * Width + tileX];
    }

    public int GetTileTypeAtPixel(int px, int py)
    {
        return GetTileTypeAt(px / TileSize, py / TileSize);
    }

    public bool IsWall(int tx, int ty)
    {
        if (tx < 0 || tx >= Width || ty < 0 || ty >= Height)
            return true;
        return (GetTileTypeAt(tx, ty) & WallMask) != 0;
    }

    public bool IsOpenSpace(int tx, int ty)
    {
        if (IsWall(tx, ty))
            return false;
        if (ty - 1 >= 0 && IsWall(tx, ty - 1))
            return false;
        return true;
    }

    public bool IsGroundBelow(int tx, int ty)
    {
        if (ty + 1 >= Height)
            return false;
        int belowType = GetTileTypeAt(tx, ty + 1);
        return (belowType & WallMask) != 0 && (belowType & 16384) == 0;
    }

    public bool IsStandableGround(int tx, int ty)
    {
        if (tx < 0 || tx >= Width || ty <= 0 || ty >= Height)
            return false;

        int groundType = GetTileTypeAt(tx, ty);
        if ((groundType & WalkableMask) == 0 || (groundType & 16384) != 0)
            return false;

        int bodyType = GetTileTypeAt(tx, ty - 1);
        return (bodyType & (SolidBodyMask | 2)) == 0;
    }

    public int GetYGround(int px, int referencePy = 0)
    {
        int tx = px / TileSize;
        if (tx < 0 || tx >= Width) return 0;

        int bestY = 0;
        int bestDist = int.MaxValue;

        for (int ty = 1; ty < Height; ty++)
        {
            if (!IsStandableGround(tx, ty))
                continue;

            int py = ty * TileSize;
            if (referencePy <= 0)
                return py;

            int dist = Math.Abs(py - referencePy);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestY = py;
            }
        }

        if (bestY > 0)
            return bestY;

        for (int ty = 0; ty < Height; ty++)
        {
            int t = GetTileTypeAt(tx, ty);
            if ((t & WalkableMask) != 0)
                return ty * TileSize;
        }

        return 0;
    }

    public (int X, int Y) ResolveSafePosition(int targetPx, int targetPy, bool preferGround = false)
    {
        if (!IsValid)
            return (targetPx, targetPy);

        int maxPx = Math.Max(TileSize, Width * TileSize - TileSize);
        int px = Math.Clamp(targetPx, TileSize, maxPx);
        int tx = Math.Clamp(px / TileSize, 0, Width - 1);

        if (targetPy <= 0 || preferGround)
        {
            return FindNearestSafeGround(px, targetPy, tx);
        }

        int py = Math.Clamp(targetPy, TileSize, Math.Max(TileSize, Height * TileSize - TileSize));
        int footTileType = GetTileTypeAtPixel(px, py);

        if ((footTileType & WalkableMask) != 0)
        {
            int snappedTy = py / TileSize;
            if (IsStandableGround(tx, snappedTy))
            {
                int snappedPy = snappedTy * TileSize;
                return (AdjustHorizontalClearance(px, snappedPy), snappedPy);
            }
        }

        if (!IsBodyBlocked(px, py) && !IsUndergroundVoid(tx, py / TileSize))
        {
            int clearPx = AdjustHorizontalClearance(px, py);
            int clearPy = AdjustCeilingClearance(clearPx, py);
            if (!IsBodyBlocked(clearPx, clearPy))
            {
                return (clearPx, clearPy);
            }
        }

        return FindNearestSafeGround(px, py, tx);
    }

    public (int X, int Y) ResolveWaypointPosition(int minX, int minY, int maxX, int maxY, int preferredX, int preferredY)
    {
        if (!IsValid)
            return (preferredX, preferredY);

        int clampedX = Math.Clamp(preferredX, minX, maxX);
        int prefTx = Math.Clamp(clampedX / TileSize, 0, Width - 1);
        int minTx = Math.Clamp(minX / TileSize, 0, Width - 1);
        int maxTx = Math.Clamp(maxX / TileSize, 0, Width - 1);
        int minTy = Math.Clamp(minY / TileSize, 1, Math.Max(1, Height - 1));
        int maxTy = Math.Clamp(maxY / TileSize, 1, Math.Max(1, Height - 1));

        for (int radius = 0; radius <= maxTx - minTx; radius++)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int cx = prefTx + radius * sign;
                if (cx < minTx || cx > maxTx)
                    continue;

                for (int ty = maxTy; ty >= minTy; ty--)
                {
                    int py = ty * TileSize;
                    if (py < minY || py > maxY)
                        continue;

                    if (IsStandableGround(cx, ty))
                    {
                        int px = cx == prefTx ? clampedX : Math.Clamp(cx * TileSize + TileSize / 2, minX, maxX);
                        return (px, py);
                    }
                }

                if (radius == 0) break;
            }
        }

        int fallbackTy = Math.Clamp(preferredY / TileSize, 1, Math.Max(1, Height - 1));
        if ((GetTileTypeAt(prefTx, fallbackTy) & WalkableMask) != 0)
        {
            int snappedY = fallbackTy * TileSize;
            if (snappedY >= minY && snappedY <= maxY)
                return (clampedX, snappedY);
        }

        for (int ty = maxTy; ty >= minTy; ty--)
        {
            int bodyType = GetTileTypeAt(prefTx, ty - 1);
            int footType = GetTileTypeAt(prefTx, ty);
            if ((bodyType & SolidBodyMask) == 0 && (footType & SolidBodyMask) == 0)
            {
                int py = Math.Clamp(ty * TileSize, minY, maxY);
                return (clampedX, py);
            }
        }

        return (clampedX, preferredY);
    }

    private (int X, int Y) FindNearestSafeGround(int px, int referencePy, int startTx)
    {
        int bestPx = px;
        int bestPy = referencePy > 0 ? referencePy : GetYGround(px);
        int bestScore = int.MaxValue;

        for (int dx = 0; dx <= 6; dx++)
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                int nx = startTx + dx * sign;
                if (nx < 1 || nx >= Width - 1)
                    continue;

                for (int ty = 1; ty < Height; ty++)
                {
                    if (!IsStandableGround(nx, ty))
                        continue;

                    int gy = ty * TileSize;
                    int gx = nx == startTx ? px : nx * TileSize + TileSize / 2;
                    gx = AdjustHorizontalClearance(gx, gy);

                    int score = Math.Abs(gx - px) * 2 + (referencePy > 0 ? Math.Abs(gy - referencePy) : 0);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestPx = gx;
                        bestPy = gy;
                    }
                }

                if (dx == 0) break;
            }

            if (bestScore < int.MaxValue && dx >= 1)
                break;
        }

        return (bestPx, bestPy);
    }

    private bool IsBodyBlocked(int px, int py)
    {
        if (py <= TileSize || py >= Height * TileSize)
            return true;

        int tx = Math.Clamp(px / TileSize, 0, Width - 1);
        int bodyTy = Math.Clamp((py - 1) / TileSize, 0, Height - 1);
        int headTy = Math.Clamp((py - 20) / TileSize, 0, Height - 1);
        int footTy = Math.Clamp(py / TileSize, 0, Height - 1);

        if ((GetTileTypeAt(tx, bodyTy) & (SolidBodyMask | WalkableMask)) != 0)
            return true;

        if ((GetTileTypeAt(tx, headTy) & SolidBodyMask) != 0)
            return true;

        int footType = GetTileTypeAt(tx, footTy);
        if ((footType & 16384) != 0)
            return true;

        if (py % TileSize != 0 && (footType & (SolidBodyMask | WalkableMask)) != 0)
            return true;

        return false;
    }

    private bool IsUndergroundVoid(int tx, int ty)
    {
        bool hasGroundAtOrBelow = false;
        for (int r = ty; r < Height; r++)
        {
            if ((GetTileTypeAt(tx, r) & WalkableMask) != 0)
            {
                hasGroundAtOrBelow = true;
                break;
            }
        }

        if (hasGroundAtOrBelow)
            return false;

        for (int r = 0; r < ty; r++)
        {
            if ((GetTileTypeAt(tx, r) & (WalkableMask | 4096)) != 0)
                return true;
        }

        return false;
    }

    private int AdjustHorizontalClearance(int px, int py)
    {
        int checkY1 = Math.Max(0, py - 1);
        int checkY2 = Math.Max(0, py - 16);

        int rightType = GetTileTypeAtPixel(px + 12, checkY1) | GetTileTypeAtPixel(px + 12, checkY2);
        if ((rightType & (4 | 4096)) != 0)
        {
            px = ((px + 12) / TileSize) * TileSize - 13;
        }

        int leftType = GetTileTypeAtPixel(px - 13, checkY1) | GetTileTypeAtPixel(px - 13, checkY2);
        if ((leftType & (8 | 4096)) != 0)
        {
            px = ((px - 13) / TileSize + 1) * TileSize + 13;
        }

        int maxPx = Math.Max(TileSize, Width * TileSize - TileSize);
        return Math.Clamp(px, TileSize, maxPx);
    }

    private int AdjustCeilingClearance(int px, int py)
    {
        if ((GetTileTypeAtPixel(px, Math.Max(0, py - 8)) & 8192) != 0)
        {
            int ceilTy = Math.Max(0, py - 8) / TileSize;
            return Math.Min(Height * TileSize - TileSize, (ceilTy + 2) * TileSize);
        }

        return py;
    }
}
