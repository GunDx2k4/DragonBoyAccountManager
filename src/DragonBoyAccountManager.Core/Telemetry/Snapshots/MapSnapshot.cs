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
    private const int SideWallMask = 4 | 8;
    private const int HardSolidMask = 4096 | 8192 | 16384 | 32768 | 65536;
    private const int SolidBodyMask = SideWallMask | HardSolidMask;
    private const int WallMask = WalkableMask | SolidBodyMask | 1;

    public static MapSnapshot Empty => new(-1, -1, string.Empty, 0, 0, []);

    public bool IsValid => MapId >= 0 && Width > 0 && Height > 0 && Types.Length >= Width * Height;

    public int GetTileTypeAt(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= Width || tileY < 0 || tileY >= Height)
            return 1000;
        int idx = tileY * Width + tileX;
        if ((uint)idx >= (uint)Types.Length)
            return 1000;
        return Types[idx];
    }

    public int GetTileTypeAtPixel(int px, int py)
    {
        if (px < 0 || py < 0)
            return 1000;
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

    public bool IsStandableGround(int tx, int ty, bool allowBorderWalls = false)
    {
        if (tx < 0 || tx >= Width || ty <= 0 || ty >= Height)
            return false;

        int groundType = GetTileTypeAt(tx, ty);
        if ((groundType & WalkableMask) == 0 || (groundType & 16384) != 0)
            return false;

        int bodyMask = allowBorderWalls || tx == 0 || tx == Width - 1
            ? HardSolidMask | 2
            : SolidBodyMask | 2;

        int bodyType = GetTileTypeAt(tx, ty - 1);
        if ((bodyType & bodyMask) != 0)
            return false;

        if (ty - 2 >= 0 && (GetTileTypeAt(tx, ty - 2) & (4096 | 16384)) != 0)
            return false;

        return true;
    }

    public int GetYGround(int px, int referencePy = 0)
    {
        if (!IsValid) return 0;
        int tx = Math.Clamp(px / TileSize, 0, Width - 1);

        if (referencePy <= 0)
        {
            for (int ty = 1; ty < Height; ty++)
            {
                if (IsStandableGround(tx, ty) && !IsBlockedBySolidGroundAbove(tx, ty))
                    return ty * TileSize;
            }

            for (int ty = 1; ty < Height; ty++)
            {
                if (IsStandableGround(tx, ty))
                    return ty * TileSize;
            }
        }
        else
        {
            int bestUnblockedY = 0;
            int bestUnblockedDist = int.MaxValue;
            int bestSameChamberY = 0;
            int bestSameChamberDist = int.MaxValue;

            int refTy = Math.Clamp(referencePy / TileSize, 0, Height - 1);
            bool refInOpenAir = !IsBodyBlocked(px, Math.Clamp(referencePy, TileSize, Math.Max(TileSize, Height * TileSize - TileSize)));

            for (int ty = 1; ty < Height; ty++)
            {
                if (!IsStandableGround(tx, ty))
                    continue;

                int py = ty * TileSize;
                int dist = Math.Abs(py - referencePy);

                if (!IsBlockedBySolidGroundAbove(tx, ty))
                {
                    if (dist < bestUnblockedDist)
                    {
                        bestUnblockedDist = dist;
                        bestUnblockedY = py;
                    }
                }
                else if (refInOpenAir && !HasSolidGroundBetween(tx, refTy, ty) && dist < bestSameChamberDist)
                {
                    bestSameChamberDist = dist;
                    bestSameChamberY = py;
                }
            }

            if (bestSameChamberY > 0 && (bestUnblockedY == 0 || bestSameChamberDist < bestUnblockedDist))
                return bestSameChamberY;

            if (bestUnblockedY > 0)
                return bestUnblockedY;
        }

        for (int ty = 1; ty < Height; ty++)
        {
            int t = GetTileTypeAt(tx, ty);
            if ((t & WalkableMask) != 0 && (GetTileTypeAt(tx, ty - 1) & HardSolidMask) == 0)
                return ty * TileSize;
        }

        return 0;
    }

    public int GetSafeStepY(int px, int lerpedPy)
    {
        if (!IsValid)
            return Math.Max(TileSize, lerpedPy);

        int maxPx = Math.Max(12, Width * TileSize - 12);
        int clampedPx = Math.Clamp(px, 12, maxPx);
        int tx = Math.Clamp(clampedPx / TileSize, 0, Width - 1);
        int maxPy = Math.Max(TileSize, Height * TileSize - TileSize);
        int clampedPy = Math.Clamp(lerpedPy, TileSize, maxPy);

        if (!IsBodyBlocked(clampedPx, clampedPy) && !IsUndergroundVoid(tx, clampedPy / TileSize))
            return clampedPy;

        int refTy = Math.Clamp(clampedPy / TileSize, 1, Height - 1);
        for (int ty = refTy; ty >= 1; ty--)
        {
            if (IsStandableGround(tx, ty) && !IsBlockedBySolidGroundAbove(tx, ty))
                return ty * TileSize;
        }

        int surfaceY = GetYGround(clampedPx);
        if (surfaceY > 0)
            return surfaceY;

        for (int ty = refTy - 1; ty >= 1; ty--)
        {
            if (IsOpenSpace(tx, ty))
                return ty * TileSize + TileSize / 2;
        }

        return clampedPy;
    }

    public (int X, int Y) ResolveSafePosition(int targetPx, int targetPy, bool preferGround = false)
    {
        if (!IsValid)
            return (targetPx, targetPy);

        int maxPx = Math.Max(12, Width * TileSize - 12);
        int px = Math.Clamp(targetPx, 12, maxPx);
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
            if (IsStandableGround(tx, snappedTy) &&
                (!IsBlockedBySolidGroundAbove(tx, snappedTy) || !HasSolidGroundBetween(tx, Math.Clamp((py - 1) / TileSize, 0, Height - 1), snappedTy)))
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

        int maxSafePx = Math.Max(12, Width * TileSize - 12);
        int normMinX = Math.Max(0, Math.Min(minX, maxX));
        int normMaxX = Math.Max(normMinX, Math.Max(minX, maxX));
        int effMinX = normMaxX >= 12 && normMinX <= maxSafePx ? Math.Max(normMinX, 12) : Math.Min(normMinX, maxSafePx);
        int effMaxX = normMaxX >= 12 && normMinX <= maxSafePx ? Math.Min(normMaxX, maxSafePx) : Math.Min(normMaxX, maxSafePx);
        if (effMinX > effMaxX)
            effMinX = effMaxX;

        int normMinY = Math.Min(minY, maxY);
        int normMaxY = Math.Max(minY, maxY);
        int clampedX = Math.Clamp(preferredX, effMinX, effMaxX);
        int prefTx = Math.Clamp(clampedX / TileSize, 0, Width - 1);
        int minTx = Math.Clamp(effMinX / TileSize, 0, Width - 1);
        int maxTx = Math.Clamp(effMaxX / TileSize, 0, Width - 1);
        int minTy = Math.Clamp(normMinY / TileSize, 1, Math.Max(1, Height - 1));
        int searchMaxTy = Math.Clamp((normMaxY + TileSize - 1) / TileSize, minTy, Math.Max(1, Height - 1));

        for (int pass = 0; pass < 2; pass++)
        {
            bool requireUnblockedSky = pass == 0;
            for (int radius = 0; radius <= maxTx - minTx; radius++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int cx = prefTx + radius * sign;
                    if (cx < minTx || cx > maxTx)
                        continue;

                    for (int ty = searchMaxTy; ty >= minTy; ty--)
                    {
                        int groundPy = ty * TileSize;
                        if (groundPy < normMinY - TileSize / 2 || groundPy > normMaxY + TileSize / 2)
                            continue;

                        if (!IsStandableGround(cx, ty, allowBorderWalls: true))
                            continue;

                        bool clearAccess = requireUnblockedSky
                            ? !IsBlockedBySolidGroundAbove(cx, ty)
                            : !HasSolidGroundBetween(cx, minTy, ty);

                        if (clearAccess)
                        {
                            int px = cx == prefTx ? clampedX : Math.Clamp(cx * TileSize + TileSize / 2, effMinX, effMaxX);
                            int py = Math.Clamp(groundPy, normMinY, normMaxY);
                            return (px, py);
                        }
                    }

                    if (radius == 0) break;
                }
            }
        }

        int fallbackTy = Math.Clamp(preferredY / TileSize, 1, Math.Max(1, Height - 1));
        if ((GetTileTypeAt(prefTx, fallbackTy) & WalkableMask) != 0 &&
            (GetTileTypeAt(prefTx, fallbackTy - 1) & HardSolidMask) == 0)
        {
            int snappedY = fallbackTy * TileSize;
            if (snappedY >= normMinY - TileSize / 2 && snappedY <= normMaxY + TileSize / 2)
                return (clampedX, Math.Clamp(snappedY, normMinY, normMaxY));
        }

        return (clampedX, Math.Clamp(preferredY, normMinY, normMaxY));
    }

    private (int X, int Y) FindNearestSafeGround(int px, int referencePy, int startTx)
    {
        int maxPy = Math.Max(TileSize, Height * TileSize - TileSize);
        int clampedRefPy = referencePy > 0 ? Math.Clamp(referencePy, TileSize, maxPy) : 0;
        int refTy = clampedRefPy > 0 ? Math.Clamp(clampedRefPy / TileSize, 0, Height - 1) : 0;
        bool refInOpenChamber = clampedRefPy > 0 && !IsBodyBlocked(px, clampedRefPy);

        int bestPx = px;
        int bestPy = clampedRefPy > 0 ? clampedRefPy : Math.Max(TileSize, GetYGround(px));
        int bestScore = int.MaxValue;

        for (int pass = 0; pass < 2; pass++)
        {
            bool requireClearAccess = pass == 0;
            for (int dx = 0; dx <= 6; dx++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    int nx = startTx + dx * sign;
                    if (nx < 0 || nx >= Width)
                        continue;

                    for (int ty = 1; ty < Height; ty++)
                    {
                        if (!IsStandableGround(nx, ty))
                            continue;

                        if (requireClearAccess)
                        {
                            bool accessible = !IsBlockedBySolidGroundAbove(nx, ty) ||
                                              (refInOpenChamber && !HasSolidGroundBetween(nx, refTy, ty));
                            if (!accessible)
                                continue;
                        }

                        int gy = ty * TileSize;
                        int gx = nx == startTx ? px : nx * TileSize + TileSize / 2;
                        gx = AdjustHorizontalClearance(gx, gy);

                        int score = Math.Abs(gx - px) * 2 + (clampedRefPy > 0 ? Math.Abs(gy - clampedRefPy) : 0);
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

            if (bestScore < int.MaxValue)
                break;
        }

        if (bestScore == int.MaxValue && clampedRefPy <= 0 && bestPy <= TileSize)
        {
            for (int ty = 1; ty < Height; ty++)
            {
                if (IsOpenSpace(startTx, ty))
                {
                    bestPy = ty * TileSize;
                    break;
                }
            }
        }

        return (bestPx, Math.Max(TileSize, bestPy));
    }

    private bool IsBlockedBySolidGroundAbove(int tx, int ty)
    {
        for (int r = ty - 1; r >= Math.Max(0, ty - 3); r--)
        {
            if ((GetTileTypeAt(tx, r) & (4096 | 16384)) != 0)
                return true;
        }

        bool hasSurfaceAbove = false;
        for (int r = 1; r < ty - 1; r++)
        {
            int t = GetTileTypeAt(tx, r);
            if (!hasSurfaceAbove)
            {
                if (IsStandableGround(tx, r))
                {
                    hasSurfaceAbove = true;
                }
            }
            else if ((t & 4096) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasSolidGroundBetween(int tx, int tyA, int tyB)
    {
        int low = Math.Clamp(Math.Min(tyA, tyB), 0, Height - 1);
        int high = Math.Clamp(Math.Max(tyA, tyB), 0, Height - 1);
        for (int r = low; r < high; r++)
        {
            if ((GetTileTypeAt(tx, r) & (4096 | 16384)) != 0)
                return true;
        }
        return false;
    }

    private bool IsBodyBlocked(int px, int py)
    {
        if (py < TileSize || py >= Height * TileSize)
            return true;

        int tx = Math.Clamp(px / TileSize, 0, Width - 1);
        int bodyTy = Math.Clamp((py - 1) / TileSize, 0, Height - 1);
        int headTy = Math.Clamp((py - 20) / TileSize, 0, Height - 1);
        int footTy = Math.Clamp(py / TileSize, 0, Height - 1);

        bool isBorderCol = tx == 0 || tx == Width - 1;
        int bodyObstacleMask = isBorderCol ? (HardSolidMask | WalkableMask) : (SolidBodyMask | WalkableMask);
        int headObstacleMask = isBorderCol ? HardSolidMask : SolidBodyMask;

        if ((GetTileTypeAt(tx, bodyTy) & bodyObstacleMask) != 0)
            return true;

        if ((GetTileTypeAt(tx, headTy) & headObstacleMask) != 0)
            return true;

        int footType = GetTileTypeAt(tx, footTy);
        if ((footType & 16384) != 0)
            return true;

        if (py % TileSize != 0 && (footType & bodyObstacleMask) != 0)
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
        int tx = Math.Clamp(px / TileSize, 0, Width - 1);
        bool isBorderCol = tx == 0 || tx == Width - 1;
        int maxPx = Math.Max(12, Width * TileSize - 12);

        if (!isBorderCol)
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
        }

        return Math.Clamp(px, 12, maxPx);
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
