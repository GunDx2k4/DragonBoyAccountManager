using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

public static class WorldMapData
{
    internal static readonly Lock Lock = new();

    public static readonly string[] PlanetTabNames = PlanetNames.All;

    public static readonly int[][] PlanetMapMatrix = Enumerable.Range(0, PlanetNames.All.Length)
        .Select(p => NextMapCatalog.Maps.Values.Where(m => m.PlanetId == p).Select(m => m.Id).ToArray())
        .ToArray();

    public static readonly int[][] LinearChains =
    [
        [ 42, 0, 1, 2, 3, 4, 5, 6 ],
        [ 3, 27, 28, 29, 30 ],
        [ 43, 7, 8, 9, 11, 12, 13, 10 ],
        [ 11, 31, 32, 33, 34 ],
        [ 52, 44, 14, 15, 16, 17, 18, 20, 19 ],
        [ 17, 35, 36, 37, 38 ],
        [ 68, 69, 70, 71, 72, 64, 65, 63, 66, 67, 73, 74, 75, 76, 77, 81, 82, 83, 79, 80 ],
        [ 80, 105 ],
        [ 109, 108, 107, 110, 106 ],
        [ 109, 105 ],
        [ 109, 106 ],
        [ 106, 107 ],
        [ 108, 105 ],
        [ 131, 132, 133 ],
        [ 102, 92, 93, 94, 96, 97, 98, 99, 100, 103 ],
        [ 53, 58, 59, 60, 61, 62, 55, 56, 54, 57 ],
        [ 149, 147, 152, 151, 148 ],
        [ 153, 156, 157, 158, 159 ],
        [ 84, 104 ],
        [ 139, 140 ]
    ];

    public static readonly Dictionary<int, List<int>> MapWaypoints = NextMapCatalog.Maps.ToDictionary(
        kv => kv.Key,
        kv => kv.Value.Adjacents.Select(a => a.TargetMapId).Distinct().ToList()
    );

    public static int GetChainWaypointSide(int currMapId, int nextMapId)
    {
        for (int c = 0; c < LinearChains.Length; c++)
        {
            int[] chain = LinearChains[c];
            for (int i = 0; i < chain.Length; i++)
            {
                if (chain[i] == currMapId)
                {
                    if (i > 0 && chain[i - 1] == nextMapId) return -1;
                    if (i < chain.Length - 1 && chain[i + 1] == nextMapId) return 1;
                }
            }
        }
        return 0;
    }

    public static int GetPlanetId(int mapId, int fallbackGender = 0)
    {
        if (NextMapCatalog.TryGetMap(mapId, out var m) && m != null)
        {
            return m.PlanetId;
        }
        return fallbackGender >= 0 && fallbackGender <= 2 ? fallbackGender : 0;
    }

    public static bool TryGetNeighbors(int mapId, out int[] neighbors)
    {
        lock (Lock)
        {
            if (MapWaypoints.TryGetValue(mapId, out var list))
            {
                neighbors = [.. list];
                return true;
            }
            neighbors = [];
            return false;
        }
    }

    public static bool RemoveNeighbor(int fromMapId, int toMapId)
    {
        lock (Lock)
        {
            if (MapWaypoints.TryGetValue(fromMapId, out var neighbors) && neighbors.Count > 1)
            {
                bool removed = neighbors.Remove(toMapId);
                if (removed)
                {
                    WorldMapPathFinder.ClearCache();
                }
                return removed;
            }
            return false;
        }
    }

    public static bool IsNpcTransitionEdge(int currMapId, int nextMapId)
    {
        if (NextMapCatalog.TryGetMap(currMapId, out var m) && m != null)
        {
            var adj = m.Adjacents.FirstOrDefault(a => a.TargetMapId == nextMapId);
            if (adj != null) return adj.IsNpc;
        }
        return NpcActions.ContainsKey((currMapId, nextMapId));
    }

    public static readonly Dictionary<(int From, int To), NpcAction> NpcActions = NextMapCatalog.Maps
        .SelectMany(kv => kv.Value.Adjacents.Where(a => a.NpcAction != null).Select(a => (Key: (kv.Key, a.TargetMapId), Value: a.NpcAction!)))
        .ToDictionary(x => x.Key, x => x.Value);

    public static readonly Dictionary<int, string> MapCatalog = NextMapCatalog.Maps.ToDictionary(
        kv => kv.Key,
        kv => kv.Value.Name
    );

    public static string GetMapName(int id) => NextMapCatalog.GetMapName(id);

    public static string RemoveAccents(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder(normalized.Length);
        foreach (char c in normalized)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c switch
                {
                    'đ' or 'Đ' => 'd',
                    _ => c
                });
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC).ToLowerInvariant().Trim();
    }

    public static string CleanName(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        s = RemoveAccents(s);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    public static bool MatchMapName(string wpName, string mapName)
    {
        if (string.IsNullOrEmpty(wpName) || string.IsNullOrEmpty(mapName)) return false;
        string w = CleanName(wpName);
        string m = CleanName(mapName);
        if (string.IsNullOrEmpty(w) || string.IsNullOrEmpty(m)) return false;

        if (w.Equals(m, StringComparison.OrdinalIgnoreCase)) return true;

        if (w.Contains('2') != m.Contains('2')) return false;
        if (w.Contains('3') != m.Contains('3')) return false;
        if (w.Contains('4') != m.Contains('4')) return false;

        string normW = RemoveAccents(wpName).ToLowerInvariant();
        string normM = RemoveAccents(mapName).ToLowerInvariant();

        bool wEastSouth = normW.Contains("dong nam");
        bool mEastSouth = normM.Contains("dong nam");
        if (wEastSouth != mEastSouth)
        {
            return false;
        }

        if ((normW.Contains("karin") || normM.Contains("karin")) && !wEastSouth && !mEastSouth)
        {
            bool wEast = normW.Contains("dong ");
            bool mEast = normM.Contains("dong ");
            if (wEast != mEast) return false;

            bool wTower = normW.Contains("thap");
            bool mTower = normM.Contains("thap");
            if (wTower != mTower) return false;

            bool wForest = normW.Contains("rung");
            bool mForest = normM.Contains("rung");
            if (wForest != mForest) return false;
        }

        if (normW.Contains("mori") || normW.Contains("moori") || normM.Contains("mori") || normM.Contains("moori"))
        {
            bool wTown = normW.Contains("thi tran");
            bool mTown = normM.Contains("thi tran");
            if (wTown != mTown) return false;

            bool wHouse = normW.Contains("nha ");
            bool mHouse = normM.Contains("nha ");
            if (wHouse != mHouse) return false;

            bool wCliff = normW.Contains("vach");
            bool mCliff = normM.Contains("vach");
            if (wCliff != mCliff) return false;
        }

        if (w.Contains(m, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        char[] sep = [' ', '-', '_', '(', ')', ',', '.', '/', '\\', ':'];
        string[] wWords = normW.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        string[] mWords = normM.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if (mWords.Length == 0 || wWords.Length == 0) return false;

        int requiredCount = 0;
        int matchedCount = 0;
        for (int i = 0; i < mWords.Length; i++)
        {
            string kw = mWords[i];
            if (kw == "t" || (kw == "dat" && normM.Contains("tram tau")))
            {
                continue;
            }
            requiredCount++;
            for (int j = 0; j < wWords.Length; j++)
            {
                if (wWords[j].Equals(kw, StringComparison.OrdinalIgnoreCase))
                {
                    matchedCount++;
                    break;
                }
            }
        }

        return requiredCount > 0 && matchedCount == requiredCount;
    }

    public static bool MatchMapId(string wpName, int targetMapId)
    {
        if (string.IsNullOrEmpty(wpName) || targetMapId < 0) return false;

        string mName = GetMapName(targetMapId);
        return MatchMapName(wpName, mName);
    }

    public static int ResolveWaypointTargetMapId(string? rawText, int currentMapId, int planetId, string? popupSays = null)
    {
        if (!string.IsNullOrEmpty(rawText))
        {
            int fromRaw = ResolveMapIdFromText(rawText, currentMapId, planetId);
            if (fromRaw >= 0) return fromRaw;
        }

        if (!string.IsNullOrEmpty(popupSays))
        {
            int fromPopup = ResolveMapIdFromText(popupSays, currentMapId, planetId);
            if (fromPopup >= 0) return fromPopup;
        }

        return -1;
    }

    public static int ResolveMapIdFromText(string text, int currentMapId, int planetId)
    {
        if (string.IsNullOrEmpty(text))
        {
            return -1;
        }

        string clean = CleanName(text);
        if (string.IsNullOrEmpty(clean))
        {
            return -1;
        }

        if (clean == "tramtauvutru" || clean == "tramtau")
        {
            if (planetId == 1 || currentMapId == 7 || currentMapId == 8 || currentMapId == 9) return 25;
            if (planetId == 2 || currentMapId == 14 || currentMapId == 15 || currentMapId == 16) return 26;
            return 24;
        }

        if (clean == "venha" || clean == "nha")
        {
            if (planetId == 1 || currentMapId == 7) return 22;
            if (planetId == 2 || currentMapId == 14) return 23;
            return 21;
        }

        if (clean == "vachnuiaru" || clean == "vacharu")
        {
            return 42;
        }
        if (clean == "vachnuimoori" || clean == "vachmoori")
        {
            return 43;
        }
        if (clean == "vachnuikakarot" || clean == "vachkakarot")
        {
            return 44;
        }

        lock (Lock)
        {
            if (MapWaypoints.TryGetValue(currentMapId, out var neighbors))
            {
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int cand = neighbors[i];
                    if (cand == currentMapId) continue;
                    if (MapCatalog.TryGetValue(cand, out var raw) && CleanName(raw).Equals(clean, StringComparison.OrdinalIgnoreCase))
                    {
                        return cand;
                    }

                }
                for (int i = 0; i < neighbors.Count; i++)
                {
                    int cand = neighbors[i];
                    if (cand != currentMapId && MatchMapId(text, cand))
                    {
                        return cand;
                    }
                }
            }
        }

        foreach (var (cand, raw) in MapCatalog)
        {
            if (cand == currentMapId || cand == 39 || cand == 40) continue;
            if (CleanName(raw).Equals(clean, StringComparison.OrdinalIgnoreCase))
            {
                return cand;
            }
        }


        if (planetId >= 0 && planetId < PlanetMapMatrix.Length)
        {
            int[] pMaps = PlanetMapMatrix[planetId];
            for (int i = 0; i < pMaps.Length; i++)
            {
                int cand = pMaps[i];
                if (cand != currentMapId && MatchMapId(text, cand))
                {
                    return cand;
                }
            }
        }

        foreach (var cand in MapCatalog.Keys)
        {
            if (cand == currentMapId || cand == 39 || cand == 40) continue;
            if (MatchMapId(text, cand))
            {
                return cand;
            }
        }

        return -1;
    }


    public static bool SyncLiveWaypoints(int currMapId, int planetId, IReadOnlyList<WorldWaypointInfo> waypoints)
    {
        if (currMapId < 0 || waypoints == null || waypoints.Count == 0)
        {
            return false;
        }

        var resolvedTargets = new List<int>();
        int totalWps = waypoints.Count;
        int resolvedCount = 0;

        for (int i = 0; i < totalWps; i++)
        {
            var wp = waypoints[i];
            int targetId = ResolveWaypointTargetMapId(wp.Name, currMapId, planetId);
            if (targetId >= 0 && targetId != currMapId)
            {
                resolvedCount++;
                if (!resolvedTargets.Contains(targetId))
                {
                    resolvedTargets.Add(targetId);
                }
            }
        }

        if (resolvedTargets.Count == 0)
        {
            return false;
        }

        bool graphChanged = false;
        lock (Lock)
        {
            if (!MapWaypoints.TryGetValue(currMapId, out var currNeighbors))
            {
                currNeighbors = [];
                MapWaypoints[currMapId] = currNeighbors;
                graphChanged = true;
            }

            bool isSpaceshipStation = NextMapCatalog.TryGetMap(currMapId, out var curMap) && curMap != null && curMap.IsSpaceshipStation;
            bool isAirMap = curMap != null && curMap.IsAirMap;
            int expectedCount = NextMapCatalog.GetExpectedWaypointCount(currMapId, planetId);

            if (!isSpaceshipStation && !isAirMap && totalWps >= expectedCount && resolvedCount == totalWps)
            {
                for (int i = currNeighbors.Count - 1; i >= 0; i--)
                {
                    int oldNeighbor = currNeighbors[i];
                    if (!resolvedTargets.Contains(oldNeighbor) && !IsNpcTransitionEdge(currMapId, oldNeighbor))
                    {
                        currNeighbors.RemoveAt(i);
                        graphChanged = true;
                    }
                }
            }

            for (int i = 0; i < resolvedTargets.Count; i++)
            {
                int targetId = resolvedTargets[i];
                if (!currNeighbors.Contains(targetId))
                {
                    currNeighbors.Add(targetId);
                    graphChanged = true;
                }

                if (!MapWaypoints.TryGetValue(targetId, out var revNeighbors))
                {
                    revNeighbors = [];
                    MapWaypoints[targetId] = revNeighbors;
                }
                if (!revNeighbors.Contains(currMapId))
                {
                    revNeighbors.Add(currMapId);
                    graphChanged = true;
                }
            }
        }

        if (graphChanged)
        {
            WorldMapPathFinder.ClearCache();
        }

        return graphChanged;
    }
}
