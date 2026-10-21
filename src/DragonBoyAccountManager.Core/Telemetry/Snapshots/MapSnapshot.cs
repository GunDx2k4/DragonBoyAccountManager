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
        return GetTileTypeAt(px / 24, py / 24);
    }

    public int GetYGround(int px)
    {
        int tx = px / 24;
        if (tx < 0 || tx >= Width) return 0;

        for (int ty = 0; ty < Height; ty++)
        {
            int t = GetTileTypeAt(tx, ty);
            if ((t & 2) == 2 || (t & 4096) == 4096 || (t & 1024) == 1024)
            {
                return ty * 24;
            }
        }
        return 0;
    }
}
