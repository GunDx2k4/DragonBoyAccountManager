namespace DragonBoyAccountManager.Core.Game.Snapshots;

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
);

public sealed record WorldBossInfo(
    int Id,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y
);

public sealed record WorldPlayerInfo(
    int Id,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y
);

public sealed record WorldWaypointInfo(
    string Name,
    int X,
    int Y,
    short MinX,
    short MinY,
    short MaxX,
    short MaxY
);

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
    int Status
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
    int TotalMobs,
    int AliveMobs,
    int CurrentPlayers,
    int MaxPlayers,
    IReadOnlyList<WorldMobInfo> Mobs,
    IReadOnlyList<WorldBossInfo> Bosses,
    IReadOnlyList<WorldPlayerInfo> Players,
    IReadOnlyList<WorldWaypointInfo> Waypoints,
    IReadOnlyList<WorldNpcInfo> Npcs,
    IReadOnlyList<WorldZoneInfo> Zones
)
{
    public static WorldSnapshot Empty => new(
        MapId: -1,
        ZoneId: -1,
        MapName: string.Empty,
        TotalMobs: 0,
        AliveMobs: 0,
        CurrentPlayers: 0,
        MaxPlayers: 15,
        Mobs: [],
        Bosses: [],
        Players: [],
        Waypoints: [],
        Npcs: [],
        Zones: []
    );
}
