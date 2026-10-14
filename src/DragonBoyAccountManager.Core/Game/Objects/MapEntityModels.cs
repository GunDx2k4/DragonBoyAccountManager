namespace DragonBoyAccountManager.Core.Game.Objects;

public sealed record MapMobInfo(
    int Id,
    int TemplateId,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y,
    int Status,
    bool IsAlive,
    bool IsBoss
);

public sealed record MapBossInfo(
    int Id,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y
);

public sealed record MapPlayerInfo(
    int Id,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y
);

public sealed record MapWaypointInfo(
    string Name,
    int X,
    int Y,
    short MinX,
    short MinY,
    short MaxX,
    short MaxY
);

public sealed record MapNpcInfo(
    int Id,
    string Name,
    int X,
    int Y
);

public sealed record MapZoneInfo(
    int ZoneId,
    int NumPlayers,
    int MaxPlayers,
    int Status
)
{
    public bool IsFull => Status == 1 || (MaxPlayers > 0 && NumPlayers >= MaxPlayers);

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

public sealed record MapSnapshot(
    int MapId,
    int ZoneId,
    string MapName,
    int TotalMobs,
    int AliveMobs,
    int CurrentPlayers,
    int MaxPlayers,
    IReadOnlyList<MapMobInfo> Mobs,
    IReadOnlyList<MapBossInfo> Bosses,
    IReadOnlyList<MapPlayerInfo> Players,
    IReadOnlyList<MapWaypointInfo> Waypoints,
    IReadOnlyList<MapNpcInfo> Npcs,
    IReadOnlyList<MapZoneInfo> Zones
)
{
    public static MapSnapshot Empty => new(
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
