using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;
using DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Items;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Dialogs;

public sealed partial class XmapGraphNodeViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PlanetName { get; init; } = string.Empty;
    public int PlanetIndex { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; init; } = 120;
    public double Height { get; init; } = 48;

    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    [ObservableProperty]
    private bool _isCurrentMap;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isOnRoute;

    [ObservableProperty]
    private bool _isHighlighted;

    public int ConnectionCount { get; init; }
    public string DisplayText => $"#{Id} {Name}";
}

public sealed partial class XmapGraphEdgeViewModel : ObservableObject
{
    public int FromId { get; init; }
    public int ToId { get; init; }
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public bool IsNpc { get; init; }

    [ObservableProperty]
    private bool _isOnRoute;
}

public sealed partial class XmapNodePickerViewModel : ObservableObject
{
    private readonly int _currentMapId;
    private readonly string _currentMapName;
    private readonly Dictionary<int, XmapGraphNodeViewModel> _allNodesMap = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    partial void OnSearchTextChanged(string value)
    {
        ApplySearchFilter();
    }

    [ObservableProperty]
    private string _selectedPlanet = PlanetNames.Earth;

    partial void OnSelectedPlanetChanged(string value)
    {
        RebuildGraph();
    }

    [ObservableProperty]
    private XmapGraphNodeViewModel? _selectedNode;

    partial void OnSelectedNodeChanged(XmapGraphNodeViewModel? oldValue, XmapGraphNodeViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
        UpdateRouteHighlights();
    }

    [ObservableProperty]
    private double _canvasWidth = 1450;

    [ObservableProperty]
    private double _canvasHeight = 850;

    [ObservableProperty]
    private string _previewSummary = "Chưa chọn bản đồ đích";

    public ObservableCollection<XmapGraphNodeViewModel> GraphNodes { get; } = [];
    public ObservableCollection<XmapGraphEdgeViewModel> GraphEdges { get; } = [];
    public ObservableCollection<XmapRouteStepItem> PreviewRouteSteps { get; } = [];

    public ObservableCollection<string> Planets { get; } = [.. PlanetNames.All, "Tất cả"];

    public bool Confirmed { get; private set; }
    public Action? RequestClose { get; set; }

    private static readonly Dictionary<int, (double X, double Y)> s_BaseCoordinates = new()
    {
        { 42, (50, 200) },
        { 0, (210, 200) },
        { 21, (210, 90) },
        { 1, (370, 200) },
        { 47, (370, 100) },
        { 111, (510, 100) },
        { 46, (370, 20) },
        { 45, (230, 20) },
        { 48, (90, 20) },
        { 50, (90, 100) },
        { 2, (530, 200) },
        { 24, (530, 320) },
        { 3, (690, 200) },
        { 27, (690, 320) },
        { 28, (850, 320) },
        { 29, (1010, 320) },
        { 30, (1170, 320) },
        { 4, (850, 200) },
        { 5, (1010, 200) },
        { 6, (1170, 200) },

        { 43, (50, 200) },
        { 7, (210, 200) },
        { 22, (210, 90) },
        { 8, (370, 200) },
        { 9, (530, 200) },
        { 25, (530, 320) },
        { 11, (690, 200) },
        { 31, (690, 90) },
        { 32, (850, 90) },
        { 33, (1010, 90) },
        { 34, (1170, 90) },
        { 12, (850, 200) },
        { 13, (1010, 200) },
        { 10, (1170, 200) },

        { 52, (50, 200) },
        { 44, (200, 200) },
        { 14, (350, 200) },
        { 23, (350, 90) },
        { 15, (500, 200) },
        { 16, (650, 200) },
        { 26, (650, 320) },
        { 17, (800, 200) },
        { 35, (800, 90) },
        { 36, (950, 90) },
        { 37, (1100, 90) },
        { 38, (1250, 90) },
        { 18, (950, 200) },
        { 20, (1100, 200) },
        { 19, (1250, 200) },

        { 68, (50, 60) },
        { 69, (190, 60) },
        { 70, (330, 60) },
        { 71, (470, 60) },
        { 72, (610, 60) },
        { 64, (750, 60) },
        { 65, (890, 60) },
        { 63, (1030, 60) },
        { 66, (1030, 160) },
        { 67, (890, 160) },
        { 73, (750, 160) },
        { 74, (610, 160) },
        { 75, (470, 160) },
        { 76, (330, 160) },
        { 77, (190, 160) },
        { 81, (50, 160) },
        { 82, (50, 260) },
        { 83, (190, 260) },
        { 79, (330, 260) },
        { 80, (470, 260) },
        { 131, (630, 260) },
        { 132, (780, 260) },
        { 133, (930, 260) },

        { 105, (60, 140) },
        { 109, (230, 60) },
        { 108, (400, 60) },
        { 107, (400, 220) },
        { 106, (230, 220) },
        { 110, (310, 320) },

        { 102, (50, 110) },
        { 92, (200, 110) },
        { 93, (350, 110) },
        { 94, (500, 110) },
        { 96, (650, 110) },
        { 97, (800, 110) },
        { 98, (950, 110) },
        { 99, (1100, 110) },
        { 100, (1250, 110) },
        { 103, (1400, 110) },

        { 84, (80, 100) },
        { 104, (260, 100) },

        { 53, (50, 50) },
        { 58, (190, 50) },
        { 59, (330, 50) },
        { 60, (470, 50) },
        { 61, (610, 50) },
        { 62, (750, 50) },
        { 55, (750, 150) },
        { 56, (610, 150) },
        { 54, (470, 150) },
        { 57, (330, 150) },

        { 149, (50, 260) },
        { 147, (200, 260) },
        { 152, (350, 260) },
        { 151, (500, 260) },
        { 148, (650, 260) },

        { 153, (50, 370) },
        { 156, (200, 370) },
        { 157, (350, 370) },
        { 158, (500, 370) },
        { 159, (650, 370) },

        { 154, (50, 480) },
        { 155, (200, 480) },
        { 166, (350, 480) },
        { 160, (500, 480) },

        { 85, (50, 590) },
        { 86, (180, 590) },
        { 87, (310, 590) },
        { 88, (440, 590) },
        { 89, (570, 590) },
        { 90, (700, 590) },
        { 91, (830, 590) }
    };

    public XmapNodePickerViewModel(int currentMapId, string currentMapName, int? initialTargetMapId = null)
    {
        _currentMapId = currentMapId;
        _currentMapName = currentMapName;

        InitializeAllNodes();

        if (initialTargetMapId.HasValue && initialTargetMapId.Value >= 0 && _allNodesMap.TryGetValue(initialTargetMapId.Value, out var initial))
        {
            _selectedPlanet = initial.PlanetName;
            _selectedNode = initial;
            initial.IsSelected = true;
        }
        else if (_allNodesMap.TryGetValue(_currentMapId, out var cur))
        {
            _selectedPlanet = cur.PlanetName;
            _selectedNode = cur;
            cur.IsSelected = true;
        }

        RebuildGraph();
    }

    private void InitializeAllNodes()
    {
        _allNodesMap.Clear();
        var allNodes = WorldMapGraph.Instance.GetAllNodes();

        foreach (var m in allNodes)
        {
            var nodeVm = new XmapGraphNodeViewModel
            {
                Id = m.Id,
                Name = m.Name,
                PlanetIndex = m.PlanetIndex,
                PlanetName = m.PlanetName,
                ConnectionCount = m.Transitions.Count,
                IsCurrentMap = m.Id == _currentMapId
            };
            _allNodesMap[m.Id] = nodeVm;
        }
    }

    private void RebuildGraph()
    {
        GraphNodes.Clear();
        GraphEdges.Clear();

        bool isAll = string.Equals(SelectedPlanet, "Tất cả", StringComparison.OrdinalIgnoreCase);

        var displayedNodes = isAll
            ? _allNodesMap.Values.ToList()
            : _allNodesMap.Values.Where(n => string.Equals(n.PlanetName, SelectedPlanet, StringComparison.OrdinalIgnoreCase)).ToList();

        if (displayedNodes.Count == 0) return;

        double maxR = 1000;
        double maxB = 700;

        foreach (var node in displayedNodes)
        {
            double baseX = 50;
            double baseY = 50;

            if (s_BaseCoordinates.TryGetValue(node.Id, out var coord))
            {
                baseX = coord.X;
                baseY = coord.Y;
            }
            else
            {
                baseX = 50 + (node.Id % 8) * 160;
                baseY = 50 + (node.Id / 8) * 90;
            }

            if (isAll)
            {
                double offsetY = node.PlanetIndex switch
                {
                    0 => 50,
                    1 => 500,
                    2 => 950,
                    3 => 1450,
                    4 => 1850,
                    5 => 2300,
                    6 => 2550,
                    _ => 2800
                };
                baseY += offsetY;
            }

            node.X = baseX;
            node.Y = baseY;

            if (node.X + node.Width + 60 > maxR) maxR = node.X + node.Width + 60;
            if (node.Y + node.Height + 60 > maxB) maxB = node.Y + node.Height + 60;

            GraphNodes.Add(node);
        }

        CanvasWidth = maxR;
        CanvasHeight = maxB;

        var activeSet = new HashSet<int>(displayedNodes.Select(n => n.Id));
        var addedPairs = new HashSet<(int, int)>();

        foreach (var node in displayedNodes)
        {
            var coreNode = WorldMapGraph.Instance.GetNode(node.Id);
            if (coreNode == null) continue;

            foreach (var trans in coreNode.Transitions)
            {
                if (!activeSet.Contains(trans.ToMapId)) continue;

                int low = Math.Min(node.Id, trans.ToMapId);
                int high = Math.Max(node.Id, trans.ToMapId);

                if (!addedPairs.Add((low, high))) continue;

                var targetNode = _allNodesMap[trans.ToMapId];

                GraphEdges.Add(new XmapGraphEdgeViewModel
                {
                    FromId = node.Id,
                    ToId = trans.ToMapId,
                    X1 = node.CenterX,
                    Y1 = node.CenterY,
                    X2 = targetNode.CenterX,
                    Y2 = targetNode.CenterY,
                    IsNpc = trans.IsNpc
                });
            }
        }

        ApplySearchFilter();
        UpdateRouteHighlights();
    }

    private void ApplySearchFilter()
    {
        string term = SearchText.Trim().ToLowerInvariant();
        bool hasSearch = !string.IsNullOrWhiteSpace(term);

        foreach (var node in GraphNodes)
        {
            node.IsHighlighted = hasSearch &&
                (node.Id.ToString().Contains(term) || node.Name.ToLowerInvariant().Contains(term));
        }
    }

    private void UpdateRouteHighlights()
    {
        PreviewRouteSteps.Clear();

        foreach (var node in _allNodesMap.Values)
        {
            node.IsOnRoute = false;
        }

        foreach (var edge in GraphEdges)
        {
            edge.IsOnRoute = false;
        }

        if (SelectedNode == null || _currentMapId < 0)
        {
            PreviewSummary = "Chưa chọn bản đồ đích";
            return;
        }

        if (_currentMapId == SelectedNode.Id)
        {
            SelectedNode.IsOnRoute = true;
            PreviewRouteSteps.Add(new XmapRouteStepItem
            {
                StepIndex = 1,
                MapId = _currentMapId,
                MapName = _currentMapName,
                TransitionText = "Hiện tại",
                DirectionText = "Tại chỗ",
                IsCurrent = true,
                IsLast = true
            });
            PreviewSummary = "Nhân vật đang đứng ở map này";
            return;
        }

        var route = WorldMapGraph.Instance.FindRoute(_currentMapId, SelectedNode.Id);
        if (route == null || route.Count == 0)
        {
            PreviewSummary = $"Không có đường nối từ #{_currentMapId} tới #{SelectedNode.Id}";
            return;
        }

        var routeMapIds = new HashSet<int> { _currentMapId };
        var routePairs = new HashSet<(int, int)>();

        PreviewRouteSteps.Add(new XmapRouteStepItem
        {
            StepIndex = 1,
            MapId = _currentMapId,
            MapName = _currentMapName,
            TransitionText = "Gốc",
            DirectionText = "Gốc",
            IsCurrent = true,
            IsLast = false
        });

        for (int i = 0; i < route.Count; i++)
        {
            var step = route[i];
            bool isLast = i == route.Count - 1;

            routeMapIds.Add(step.TargetMapId);
            routePairs.Add((Math.Min(step.FromMapId, step.TargetMapId), Math.Max(step.FromMapId, step.TargetMapId)));

            string dirText = step.Transition.Direction switch
            {
                MapDirection.Left => "Trái",
                MapDirection.Right => "Phải",
                MapDirection.Center => "Giữa",
                MapDirection.Npc => "NPC",
                _ => "Cổng"
            };

            PreviewRouteSteps.Add(new XmapRouteStepItem
            {
                StepIndex = i + 2,
                MapId = step.TargetMapId,
                MapName = step.TargetMapName,
                TransitionText = step.Transition.IsNpc ? "NPC" : "Cổng",
                DirectionText = dirText,
                IsCurrent = false,
                IsLast = isLast
            });
        }

        foreach (var node in GraphNodes)
        {
            if (routeMapIds.Contains(node.Id))
            {
                node.IsOnRoute = true;
            }
        }

        foreach (var edge in GraphEdges)
        {
            int low = Math.Min(edge.FromId, edge.ToId);
            int high = Math.Max(edge.FromId, edge.ToId);
            if (routePairs.Contains((low, high)))
            {
                edge.IsOnRoute = true;
            }
        }

        PreviewSummary = $"{route.Count} chặng: #{_currentMapId} -> #{SelectedNode.Id}";
    }

    [RelayCommand]
    public void SelectNode(XmapGraphNodeViewModel? node)
    {
        if (node != null)
        {
            SelectedNode = node;
        }
    }

    [RelayCommand]
    public void Confirm()
    {
        if (SelectedNode != null)
        {
            Confirmed = true;
            RequestClose?.Invoke();
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        Confirmed = false;
        RequestClose?.Invoke();
    }
}
