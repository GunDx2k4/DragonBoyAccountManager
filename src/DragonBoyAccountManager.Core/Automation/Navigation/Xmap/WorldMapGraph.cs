using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public sealed class WorldMapGraph
{
    private static readonly Lazy<WorldMapGraph> _instance = new(() => new WorldMapGraph());
    public static WorldMapGraph Instance => _instance.Value;

    private readonly Lock _lock = new();
    private readonly Dictionary<int, MapNode> _nodes = [];
    private readonly List<PlanetInfo> _planets = [];

    public IReadOnlyList<PlanetInfo> Planets => _planets;

    public static string[] PlanetNames => WorldMapData.PlanetTabNames;
    public static int[][] PlanetMapMatrix => WorldMapData.PlanetMapMatrix;
    public static int[][] LinearChains => WorldMapData.LinearChains;
    public static Dictionary<(int From, int To), NpcAction> NpcActions => WorldMapData.NpcActions;

    public WorldMapGraph()
    {
        Initialize();
    }

    public void ClearPathCache() => WorldMapPathFinder.ClearCache();

    public void Initialize()
    {
        lock (_lock)
        {
            _nodes.Clear();
            _planets.Clear();

            for (int p = 0; p < WorldMapData.PlanetTabNames.Length; p++)
            {
                string pName = WorldMapData.PlanetTabNames[p];
                int[] pMapIds = WorldMapData.PlanetMapMatrix[p];
                _planets.Add(new PlanetInfo(p, pName, pMapIds));
            }

            foreach (var (mapId, mapData) in NextMapCatalog.Maps)
            {
                var transitions = mapData.Adjacents.Select(a => new MapTransition(
                    mapId,
                    a.TargetMapId,
                    a.TargetMapName,
                    a.Direction,
                    a.NpcAction
                )).ToList();

                _nodes[mapId] = new MapNode
                {
                    Id = mapId,
                    Name = mapData.Name,
                    PlanetIndex = mapData.PlanetId,
                    PlanetName = mapData.PlanetName,
                    Transitions = transitions
                };
            }
        }
    }

    public MapNode? GetNode(int mapId)
    {
        lock (_lock)
        {
            return _nodes.TryGetValue(mapId, out var node) ? node : null;
        }
    }

    public IReadOnlyList<MapNode> GetAllNodes()
    {
        lock (_lock)
        {
            return _nodes.Values.OrderBy(m => m.PlanetIndex).ThenBy(m => m.Id).ToList();
        }
    }

    public IReadOnlyList<MapNode> GetNodesByPlanet(string planetName)
    {
        lock (_lock)
        {
            return _nodes.Values.Where(m => string.Equals(m.PlanetName, planetName, StringComparison.OrdinalIgnoreCase))
                                .OrderBy(m => m.Id)
                                .ToList();
        }
    }

    public MapDirection ResolveDirection(
        int fromMapId,
        int toMapId,
        bool isNpc = false,
        IReadOnlyList<WorldWaypointInfo>? liveWaypoints = null)
    {
        if (isNpc || NpcActions.ContainsKey((fromMapId, toMapId)) || WorldMapData.IsNpcTransitionEdge(fromMapId, toMapId))
        {
            return MapDirection.Npc;
        }

        if (liveWaypoints != null && liveWaypoints.Count > 0)
        {
            string targetName = WorldMapData.GetMapName(toMapId);
            var matchingWp = liveWaypoints.FirstOrDefault(w =>
                !string.IsNullOrWhiteSpace(w.Name) && (w.Name == targetName || WorldMapData.MatchMapName(w.Name, targetName)));

            if (matchingWp != null)
            {
                if (matchingWp.MinX <= 120) return MapDirection.Left;
                if (matchingWp.MaxX >= 800) return MapDirection.Right;
                return MapDirection.Center;
            }
        }

        int side = WorldMapData.GetChainWaypointSide(fromMapId, toMapId);
        if (side < 0) return MapDirection.Left;
        if (side > 0) return MapDirection.Right;

        return MapDirection.Center;
    }

    public List<NavigationStep>? FindRoute(
        int startMapId,
        int targetMapId,
        int gender = -1,
        int taskId = -1,
        long power = 0,
        bool hasClan = true)
    {
        var rawPath = WorldMapPathFinder.FindPath(startMapId, targetMapId, gender, taskId, power, hasClan);
        if (rawPath == null) return null;
        if (rawPath.Count == 0) return [];

        var steps = new List<NavigationStep>();
        int current = startMapId;

        for (int i = 0; i < rawPath.Count; i++)
        {
            int next = rawPath[i];
            string targetName = WorldMapData.GetMapName(next);

            NpcActions.TryGetValue((current, next), out var npc);
            bool isNpc = npc != null || WorldMapData.IsNpcTransitionEdge(current, next);
            var dir = ResolveDirection(current, next, isNpc);

            var transition = new MapTransition(
                current,
                next,
                targetName,
                dir,
                npc
            );

            steps.Add(new NavigationStep(i + 1, current, next, targetName, transition));
            current = next;
        }

        return steps;
    }

    public void RegisterDynamicConnection(
        int fromMapId,
        int toMapId,
        string? toMapName = null,
        NpcAction? npc = null,
        int? wpX = null,
        int? wpY = null)
    {
        if (fromMapId < 0 || toMapId < 0 || fromMapId == toMapId) return;

        lock (_lock)
        {
            if (!_nodes.TryGetValue(fromMapId, out var fromNode))
            {
                string fnName = WorldMapData.GetMapName(fromMapId);
                fromNode = new MapNode
                {
                    Id = fromMapId,
                    Name = fnName,
                    PlanetIndex = WorldMapData.PlanetTabNames.Length - 1,
                    PlanetName = WorldMapData.PlanetTabNames[^1]
                };
                _nodes[fromMapId] = fromNode;
            }

            if (!_nodes.TryGetValue(toMapId, out var toNode))
            {
                string tnName = toMapName ?? WorldMapData.GetMapName(toMapId);
                toNode = new MapNode
                {
                    Id = toMapId,
                    Name = tnName,
                    PlanetIndex = WorldMapData.PlanetTabNames.Length - 1,
                    PlanetName = WorldMapData.PlanetTabNames[^1]
                };
                _nodes[toMapId] = toNode;
            }

            bool isNpc = npc != null || WorldMapData.IsNpcTransitionEdge(fromMapId, toMapId);
            var dir = ResolveDirection(fromMapId, toMapId, isNpc);
            var wp = wpX.HasValue && wpY.HasValue ? (wpX.Value, wpY.Value) : ((int, int)?)null;

            var newTransition = new MapTransition(
                fromMapId,
                toMapId,
                toNode.Name,
                dir,
                npc,
                wp
            );

            var list = fromNode.Transitions.ToList();
            int existingIdx = list.FindIndex(t => t.ToMapId == toMapId);
            if (existingIdx >= 0)
            {
                list[existingIdx] = newTransition;
            }
            else
            {
                list.Add(newTransition);
            }
            fromNode.Transitions = list;

            lock (WorldMapData.Lock)
            {
                if (!WorldMapData.MapWaypoints.TryGetValue(fromMapId, out var neighbors))
                {
                    neighbors = [];
                    WorldMapData.MapWaypoints[fromMapId] = neighbors;
                }
                if (!neighbors.Contains(toMapId))
                {
                    neighbors.Add(toMapId);
                }
            }
        }

        WorldMapPathFinder.ClearCache();
    }
}
