namespace DragonBoyAccountManager.Core.Telemetry.Snapshots;

public sealed record WorldMobInfo(
    int Id,
    int TemplateId,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y,
    int Status,
    bool IsAlive,
    bool IsBoss,
    bool IsSuperMob = false,
    bool IsFlying = false
)
{
    public double DistanceTo(int targetX, int targetY)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed record WorldBossInfo(
    int Id,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y
)
{
    public double DistanceTo(int targetX, int targetY)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed record WorldPlayerInfo(
    int Id,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y
)
{
    public double DistanceTo(int targetX, int targetY)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed record WorldWaypointInfo(
    string Name,
    int X,
    int Y,
    short MinX,
    short MinY,
    short MaxX,
    short MaxY
)
{
    public double DistanceTo(int targetX, int targetY)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed record WorldNpcInfo(
    int Id,
    string Name,
    int X,
    int Y
);

public sealed record WorldZoneInfo(
    int ZoneId,
    int NumPlayers,
    int MaxPlayers,
    int Status = 0
)
{
    public bool IsFull => Status >= 2 || (MaxPlayers > 0 && NumPlayers >= MaxPlayers);

    public string DisplayText
    {
        get
        {
            if (MaxPlayers <= 0) return $"Khu {ZoneId}";
            if (IsFull) return $"Khu {ZoneId} ({NumPlayers}/{MaxPlayers}) - Đầy";
            return $"Khu {ZoneId} ({NumPlayers}/{MaxPlayers})";
        }
    }

    public override string ToString() => DisplayText;
}

public sealed record WorldSnapshot(
    int MapId,
    int ZoneId,
    string MapName,
    IReadOnlyList<WorldMobInfo> Mobs,
    IReadOnlyList<WorldBossInfo> Bosses,
    IReadOnlyList<WorldPlayerInfo> Players,
    IReadOnlyList<WorldNpcInfo> Npcs,
    IReadOnlyList<WorldWaypointInfo> Waypoints,
    IReadOnlyList<GroundItemSnapshot> GroundItems,
    IReadOnlyList<WorldZoneInfo> Zones
)
{
    public int TotalMobs => Mobs.Count;
    public int AliveMobs => Mobs.Count(m => m.IsAlive);
    public int CurrentPlayers => Players.Count;
    public int MaxPlayers => 15;

    public static WorldSnapshot Empty => new(
        -1, -1, string.Empty,
        [], [], [], [], [], [], []);
}
