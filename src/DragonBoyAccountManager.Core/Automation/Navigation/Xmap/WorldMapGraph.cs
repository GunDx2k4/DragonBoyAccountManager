using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public sealed class WorldMapGraph
{
    private readonly record struct PathKey(
        int Start,
        int Target,
        int Gender,
        int TaskId,
        int PowerTier,
        bool HasClan);

    private static readonly Lazy<WorldMapGraph> _instance = new(() => new WorldMapGraph());
    public static WorldMapGraph Instance => _instance.Value;

    public static readonly Dictionary<(int From, int To), NpcAction> NpcActions = NextMapCatalog.Maps
        .SelectMany(kv => kv.Value.Adjacents.Where(a => a.NpcAction != null).Select(a => (Key: (kv.Key, a.TargetMapId), Value: a.NpcAction!)))
        .ToDictionary(x => x.Key, x => x.Value);

    private readonly Dictionary<int, MapNode> _nodes = [];
    private readonly ConcurrentDictionary<PathKey, int[]> _pathCache = new();

    public WorldMapGraph()
    {
        Initialize();
    }

    public static string GetMapName(int id) => NextMapCatalog.GetMapName(id);

    public static bool IsNpcTransitionEdge(int currMapId, int nextMapId)
    {
        if (NextMapCatalog.TryGetMap(currMapId, out var m) && m != null)
        {
            var adj = m.Adjacents.FirstOrDefault(a => a.TargetMapId == nextMapId);
            if (adj != null) return adj.IsNpc;
        }
        return NpcActions.ContainsKey((currMapId, nextMapId));
    }

    public static WorldWaypointInfo? FindWaypoint(
        int fromMapId,
        int toMapId,
        IReadOnlyList<WorldWaypointInfo>? waypoints,
        int expectedWaypointCount = 0)
    {
        if (waypoints == null || waypoints.Count == 0) return null;
        if (expectedWaypointCount > 0 && waypoints.Count < expectedWaypointCount) return null;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(waypoints[i].Name)) return null;
        }

        string targetName = GetMapName(toMapId);
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i].Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
            {
                return waypoints[i];
            }
        }

        if (!NextMapCatalog.TryGetMap(fromMapId, out var mapData) || mapData == null)
        {
            return null;
        }

        var adj = mapData.Adjacents.FirstOrDefault(a => a.TargetMapId == toMapId && !a.IsNpc);
        if (adj == null)
        {
            return null;
        }

        return adj.Direction switch
        {
            MapDirection.Left => waypoints[0],
            MapDirection.Right => waypoints[^1],
            MapDirection.Center => waypoints.Count >= 3 ? waypoints[1] : waypoints[0],
            _ => null
        };
    }

    public void Initialize()
    {
        _nodes.Clear();
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

        _pathCache.Clear();
    }

    public MapNode? GetNode(int mapId) =>
        _nodes.TryGetValue(mapId, out var node) ? node : null;

    public IReadOnlyList<MapNode> GetAllNodes() =>
        _nodes.Values.OrderBy(m => m.PlanetIndex).ThenBy(m => m.Id).ToList();

    public List<int>? FindPath(
        int startMapId,
        int targetMapId,
        int gender = -1,
        int taskId = -1,
        long power = 0,
        bool hasClan = true)
    {
        if (startMapId == targetMapId)
        {
            return [];
        }

        int powerTier = power <= 0L ? 0 : (power >= 60_000_000_000L ? 3 : (power >= 40_000_000_000L ? 2 : 1));
        var key = new PathKey(startMapId, targetMapId, gender, taskId, powerTier, hasClan);

        if (_pathCache.TryGetValue(key, out var cached))
        {
            return [.. cached];
        }

        bool isColdEndpoint = (startMapId >= 105 && startMapId <= 110) || (targetMapId >= 105 && targetMapId <= 110);

        var queue = new Queue<int>();
        var parent = new Dictionary<int, int>();
        var visited = new HashSet<int>();

        queue.Enqueue(startMapId);
        visited.Add(startMapId);

        bool found = false;
        while (queue.Count > 0)
        {
            int curr = queue.Dequeue();
            if (curr == targetMapId)
            {
                found = true;
                break;
            }

            if (!NextMapCatalog.TryGetMap(curr, out var mapData) || mapData == null)
            {
                continue;
            }

            var adjacents = mapData.Adjacents;
            for (int i = 0; i < adjacents.Count; i++)
            {
                int next = adjacents[i].TargetMapId;

                if ((next == 21 || next == 39) && gender >= 0 && gender != 0 && targetMapId != next) continue;
                if ((next == 22 || next == 40) && gender >= 0 && gender != 1 && targetMapId != next) continue;
                if ((next == 23 || next == 41) && gender >= 0 && gender != 2 && targetMapId != next) continue;

                if (next >= 92 && next <= 103 && taskId >= 0 && taskId <= 24 && targetMapId != next) continue;
                if (next >= 105 && next <= 110)
                {
                    if (taskId >= 0 && taskId <= 30 && targetMapId != next) continue;
                    if (!isColdEndpoint) continue;
                }

                if (!hasClan && ((next >= 53 && next <= 62) || (next >= 147 && next <= 153 && next != 150) || (next >= 156 && next <= 159)))
                {
                    continue;
                }

                if (power > 0L)
                {
                    if (next >= 153 && next <= 159 && power < 40_000_000_000L && targetMapId != next) continue;
                    if ((next == 155 || next == 166) && power < 60_000_000_000L && targetMapId != next) continue;
                }

                if (visited.Add(next))
                {
                    parent[next] = curr;
                    queue.Enqueue(next);
                }
            }
        }

        if (!found)
        {
            return null;
        }

        var path = new List<int>();
        int currNode = targetMapId;
        int maxSteps = 100;
        while (currNode != startMapId && maxSteps-- > 0 && parent.TryGetValue(currNode, out int prev))
        {
            path.Insert(0, currNode);
            currNode = prev;
        }

        if (_pathCache.Count < 500)
        {
            _pathCache[key] = [.. path];
        }

        return path;
    }

    public List<NavigationStep>? FindRoute(
        int startMapId,
        int targetMapId,
        int gender = -1,
        int taskId = -1,
        long power = 0,
        bool hasClan = true)
    {
        var rawPath = FindPath(startMapId, targetMapId, gender, taskId, power, hasClan);
        if (rawPath == null) return null;
        if (rawPath.Count == 0) return [];

        var steps = new List<NavigationStep>(rawPath.Count);
        int current = startMapId;

        for (int i = 0; i < rawPath.Count; i++)
        {
            int next = rawPath[i];
            string targetName = GetMapName(next);

            NpcActions.TryGetValue((current, next), out var npc);
            var dir = MapDirection.Center;
            if (npc != null || IsNpcTransitionEdge(current, next))
            {
                dir = MapDirection.Npc;
            }
            else if (NextMapCatalog.TryGetMap(current, out var mapData) && mapData != null)
            {
                var adj = mapData.Adjacents.FirstOrDefault(a => a.TargetMapId == next && !a.IsNpc);
                if (adj != null) dir = adj.Direction;
            }

            var transition = new MapTransition(current, next, targetName, dir, npc);
            steps.Add(new NavigationStep(i + 1, current, next, targetName, transition));
            current = next;
        }

        return steps;
    }
}
