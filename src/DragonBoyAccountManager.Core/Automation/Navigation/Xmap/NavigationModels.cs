using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public enum MapDirection
{
    Left = 0,
    Center = 1,
    Right = 2,
    Npc = 3
}

public sealed record NpcAction(
    short NpcTemplateId,
    sbyte Select,
    int[]? SelectChain = null
);

public sealed record MapTransition(
    int FromMapId,
    int ToMapId,
    string ToMapName,
    MapDirection Direction,
    NpcAction? Npc = null,
    (int X, int Y)? Waypoint = null
)
{
    public bool IsNpc => Npc != null || Direction == MapDirection.Npc;
}

public sealed class MapNode
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int PlanetIndex { get; init; }
    public string PlanetName { get; init; } = string.Empty;
    public IReadOnlyList<MapTransition> Transitions { get; set; } = [];

    public override string ToString() => $"#{Id} {Name} ({PlanetName})";
}

public static class PlanetNames
{
    public const string Earth = "Trái Đất";
    public const string Namek = "Namếc";
    public const string Saiyan = "Xayda";
    public const string Nappa = "Nappa";
    public const string Cold = "Cooler";
    public const string Future = "Tương Lai";
    public const string Supermarket = "Siêu Thị";
    public const string Other = "Khác/NRSD";

    public static readonly string[] All =
    [
        Earth,
        Namek,
        Saiyan,
        Nappa,
        Cold,
        Future,
        Supermarket,
        Other
    ];

    public static string GetName(int planetId) =>
        planetId >= 0 && planetId < All.Length ? All[planetId] : Other;
}

public enum NextMapTransitionType
{
    Waypoint = 0,
    AirMapDownward = 1,
    Npc = 2
}

public sealed record NextMapAdjacent(
    int TargetMapId,
    NextMapTransitionType TransitionType = NextMapTransitionType.Waypoint,
    MapDirection Direction = MapDirection.Center,
    NpcAction? NpcAction = null,
    long RequiredPower = 0,
    int AllowedGender = -1
)
{
    public bool IsNpc => TransitionType == NextMapTransitionType.Npc;
    public string TargetMapName => NextMapCatalog.GetMapName(TargetMapId);

    public static NextMapAdjacent Waypoint(int targetMapId, MapDirection direction = MapDirection.Center) =>
        new(targetMapId, NextMapTransitionType.Waypoint, direction);

    public static NextMapAdjacent AirDownward(int targetMapId) =>
        new(targetMapId, NextMapTransitionType.AirMapDownward, MapDirection.Center);

    public static NextMapAdjacent Npc(int targetMapId, short npcTemplateId, sbyte select, int[]? selectChain = null) =>
        new(targetMapId, NextMapTransitionType.Npc, MapDirection.Npc, new NpcAction(npcTemplateId, select, selectChain));
}

public sealed class NextMapData
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required int PlanetId { get; init; }
    public string PlanetName => PlanetNames.GetName(PlanetId);
    public bool IsAirMap { get; init; }
    public bool IsSpaceshipStation { get; init; }
    public List<NextMapAdjacent> Adjacents { get; init; } = [];

    public override string ToString() => $"#{Id} {Name} ({PlanetName}, {Adjacents.Count} edges)";
}

public sealed record PlanetInfo(
    int Index,
    string Name,
    IReadOnlyList<int> MapIds
);

public sealed record NavigationStep(
    int StepIndex,
    int FromMapId,
    int TargetMapId,
    string TargetMapName,
    MapTransition Transition
);
