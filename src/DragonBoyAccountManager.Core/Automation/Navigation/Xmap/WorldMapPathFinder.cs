namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public static class WorldMapPathFinder
{
    private static readonly Lock _lock = new();
    private static readonly Dictionary<long, List<int>> _pathCache = [];

    public static void ClearCache()
    {
        lock (_lock)
        {
            _pathCache.Clear();
        }
    }

    public static List<int>? FindPath(
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

        int powerTier = power >= 60_000_000_000L ? 2 : (power >= 40_000_000_000L ? 1 : 0);
        int taskKey = taskId < 0 ? 63 : (taskId > 62 ? 62 : taskId);
        long cacheKey = ((long)startMapId << 40)
            | ((hasClan ? 1L : 0L) << 32)
            | ((long)targetMapId << 16)
            | ((long)taskKey << 8)
            | ((long)powerTier << 4)
            | (byte)(gender + 1);

        lock (_lock)
        {
            if (_pathCache.TryGetValue(cacheKey, out var cachedPath))
            {
                return [.. cachedPath];
            }
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

            if (WorldMapData.TryGetNeighbors(curr, out var neighbors))
            {
                for (int i = 0; i < neighbors.Length; i++)
                {
                    int next = neighbors[i];

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

                    if (!visited.Contains(next))
                    {
                        visited.Add(next);
                        parent[next] = curr;
                        queue.Enqueue(next);
                    }
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

        lock (_lock)
        {
            if (_pathCache.Count < 500)
            {
                _pathCache[cacheKey] = [.. path];
            }
        }

        return path;
    }
}
