using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;
using DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Dialogs;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities;

public sealed partial class MapUtilitiesTabViewModel
{
    [ObservableProperty]
    private int _focalMapId = -1;

    [ObservableProperty]
    private string _focalMapName = "Chưa chọn bản đồ";

    [ObservableProperty]
    private string _focalPlanetName = string.Empty;

    [ObservableProperty]
    private string _focalMapCoordinates = string.Empty;

    [ObservableProperty]
    private bool _isFocalAtCurrentMap = true;

    [ObservableProperty]
    private string _quickTargetMapIdInput = string.Empty;

    public ObservableCollection<XmapConnectedNodeItem> AllAdjacentNodes { get; } = [];

    [ObservableProperty]
    private string _xmapSearchText = string.Empty;

    partial void OnXmapSearchTextChanged(string value)
    {
        ApplyXmapFilter();
    }

    [ObservableProperty]
    private string _selectedPlanetFilter = "Tất cả";

    partial void OnSelectedPlanetFilterChanged(string value)
    {
        ApplyXmapFilter();
    }

    [ObservableProperty]
    private XmapMapItemViewModel? _selectedXmapTarget;

    partial void OnSelectedXmapTargetChanged(XmapMapItemViewModel? oldValue, XmapMapItemViewModel? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
        UpdateRouteSteps();
        UpdateRouteHighlightsOnSpatialNodes();
    }

    [ObservableProperty]
    private string _routeSummary = string.Empty;

    public ObservableCollection<XmapRouteStepItem> CurrentRouteSteps { get; } = [];

    [ObservableProperty]
    private bool _isNavigatingXmap;

    [ObservableProperty]
    private string _xmapStatus = string.Empty;

    private CancellationTokenSource? _xmapCts;

    public ObservableCollection<XmapMapItemViewModel> FilteredXmapMaps { get; } = [];
    public ObservableCollection<string> PlanetFilters { get; } = ["Tất cả", .. PlanetNames.All];
    private readonly List<XmapMapItemViewModel> _allXmapMaps = [];

    private static int GetDirectionOrder(MapDirection direction) => direction switch
    {
        MapDirection.Left => 0,
        MapDirection.Center => 1,
        MapDirection.Right => 2,
        MapDirection.Npc => 3,
        _ => 4
    };

    public void InitializeXmapData()
    {
        _allXmapMaps.Clear();
        var nodes = WorldMapGraph.Instance.GetAllNodes();
        foreach (var m in nodes)
        {
            var connected = m.Transitions.Select(t =>
            {
                string dirLabel = t.Direction switch
                {
                    MapDirection.Left => "◄ Trái",
                    MapDirection.Right => "► Phải",
                    MapDirection.Center => "▲ Giữa",
                    MapDirection.Npc => "★ NPC",
                    _ => "Cổng"
                };

                return new XmapConnectedNodeItem
                {
                    FromMapId = m.Id,
                    TargetMapId = t.ToMapId,
                    TargetMapName = t.ToMapName,
                    TransitionType = t.Npc != null ? "NPC" : "Cổng",
                    Direction = t.Direction,
                    DirectionLabel = dirLabel
                };
            })
            .OrderBy(c => GetDirectionOrder(c.Direction))
            .ToList();

            _allXmapMaps.Add(new XmapMapItemViewModel
            {
                Id = m.Id,
                Name = m.Name,
                PlanetId = m.PlanetIndex,
                PlanetName = m.PlanetName,
                ConnectedNodes = connected,
                IsCurrentMap = m.Id == CurrentMapId
            });
        }
        ApplyXmapFilter();
        UpdateRouteSteps();

        if (CurrentMapId >= 0)
        {
            SetFocalMap(CurrentMapId, isUserExploration: false);
        }
    }

    public void SetFocalMap(int mapId, bool isUserExploration = false)
    {
        if (mapId < 0) return;

        FocalMapId = mapId;
        IsFocalAtCurrentMap = mapId == CurrentMapId;

        var mapNode = WorldMapGraph.Instance.GetNode(mapId);
        if (mapNode != null)
        {
            FocalMapName = mapNode.Name;
            FocalPlanetName = mapNode.PlanetName;
        }
        else
        {
            FocalMapName = $"Map #{mapId}";
            FocalPlanetName = string.Empty;
        }

        if (IsFocalAtCurrentMap)
        {
            FocalMapCoordinates = $"Khu {CurrentZoneId}";
        }
        else
        {
            FocalMapCoordinates = isUserExploration ? "(Đang duyệt)" : string.Empty;
        }

        var adjacentNodes = new List<XmapConnectedNodeItem>();

        if (mapNode != null)
        {
            var liveWps = IsFocalAtCurrentMap ? Waypoints.ToList() : null;

            foreach (var t in mapNode.Transitions)
            {
                var dir = t.Direction;
                string dirLabel = dir switch
                {
                    MapDirection.Left => "Cổng Trái",
                    MapDirection.Right => "Cổng Phải",
                    MapDirection.Center => "Cổng Giữa",
                    MapDirection.Npc => "NPC",
                    _ => "Cổng"
                };

                string coord = string.Empty;
                if (IsFocalAtCurrentMap && liveWps != null && !t.IsNpc)
                {
                    var matchingWp = WorldMapGraph.FindWaypoint(mapId, t.ToMapId, liveWps);
                    if (matchingWp != null)
                    {
                        coord = $"({matchingWp.X}, {matchingWp.Y})";
                    }
                }

                adjacentNodes.Add(new XmapConnectedNodeItem
                {
                    FromMapId = mapId,
                    TargetMapId = t.ToMapId,
                    TargetMapName = t.ToMapName,
                    TransitionType = t.Npc != null ? "NPC" : "Cổng",
                    Direction = dir,
                    DirectionLabel = dirLabel,
                    CoordinateDisplay = coord
                });
            }
        }

        adjacentNodes.Sort((a, b) => GetDirectionOrder(a.Direction).CompareTo(GetDirectionOrder(b.Direction)));
        AllAdjacentNodes.SyncWith(adjacentNodes);
        UpdateRouteHighlightsOnSpatialNodes();
    }

    private void UpdateRouteHighlightsOnSpatialNodes()
    {
        int nextTargetMapId = -1;
        if (CurrentRouteSteps.Count > 1)
        {
            var nextStep = CurrentRouteSteps.Skip(1).FirstOrDefault();
            if (nextStep != null)
            {
                nextTargetMapId = nextStep.MapId;
            }
        }

        foreach (var node in AllAdjacentNodes)
        {
            node.IsOnRoute = node.TargetMapId == nextTargetMapId;
        }
    }

    public void UpdateCurrentMapFlags()
    {
        foreach (var map in _allXmapMaps)
        {
            map.IsCurrentMap = map.Id == CurrentMapId;
        }
        UpdateRouteSteps();
    }

    public void UpdateRouteSteps()
    {
        CurrentRouteSteps.Clear();
        if (SelectedXmapTarget == null || CurrentMapId < 0)
        {
            RouteSummary = string.Empty;
            return;
        }

        if (CurrentMapId == SelectedXmapTarget.Id)
        {
            CurrentRouteSteps.Add(new XmapRouteStepItem
            {
                StepIndex = 1,
                MapId = CurrentMapId,
                MapName = CurrentMapName,
                TransitionText = "Hiện tại",
                DirectionText = "Tại chỗ",
                IsCurrent = true,
                IsLast = true
            });
            RouteSummary = "Đã ở map mục tiêu.";
            return;
        }

        var route = WorldMapGraph.Instance.FindRoute(CurrentMapId, SelectedXmapTarget.Id);
        if (route == null || route.Count == 0)
        {
            RouteSummary = $"Không tìm thấy lộ trình #{CurrentMapId} -> #{SelectedXmapTarget.Id}";
            return;
        }

        XmapRouteStepItem.PopulateRouteSteps(CurrentRouteSteps, CurrentMapId, CurrentMapName, route);
        RouteSummary = $"{route.Count} chặng: #{CurrentMapId} -> #{SelectedXmapTarget.Id}";
    }

    private void ApplyXmapFilter()
    {
        var query = _allXmapMaps.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SelectedPlanetFilter) && SelectedPlanetFilter != "Tất cả")
        {
            query = query.Where(m => string.Equals(m.PlanetName, SelectedPlanetFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(XmapSearchText))
        {
            string term = XmapSearchText.Trim().ToLowerInvariant();
            query = query.Where(m => m.Id.ToString().Contains(term) || m.Name.ToLowerInvariant().Contains(term));
        }

        FilteredXmapMaps.SyncWith(query.ToList());
    }

    private void ClearXmapFocalState()
    {
        FocalMapId = -1;
        FocalMapName = "Chưa chọn bản đồ";
        FocalPlanetName = string.Empty;
        FocalMapCoordinates = string.Empty;
        AllAdjacentNodes.Clear();
        UpdateCurrentMapFlags();
    }

    private static int ResolveTargetMapId(object? parameter) => parameter switch
    {
        XmapConnectedNodeItem item => item.TargetMapId,
        int id => id,
        _ => -1
    };

    [RelayCommand]
    public async Task StartXmap(object? parameter)
    {
        XmapMapItemViewModel? target = parameter as XmapMapItemViewModel ?? SelectedXmapTarget;
        if (target == null)
        {
            StatusText = "Chưa chọn map đích!";
            return;
        }

        SelectedXmapTarget = target;

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        _xmapCts?.Cancel();
        var cts = new CancellationTokenSource();
        var token = cts.Token;
        _xmapCts = cts;

        IsNavigatingXmap = true;
        XmapStatus = $"Đang Xmap tới {target.DisplayText}...";
        StatusText = $"Bắt đầu Xmap tới {target.DisplayText} ({targetIds.Count} tài khoản)";

        bool isTeleport = FastTeleport;

        try
        {
            var tasks = targetIds.Select(async id =>
            {
                var res = await _gameEngine.XmapMoveAsync(id, target.Id, isTeleport, token);
                return res;
            });

            var results = await Task.WhenAll(tasks);
            int successes = results.Count(r => r.IsSuccess);

            if (!token.IsCancellationRequested)
            {
                StatusText = $"Xmap tới {target.DisplayText}: {successes}/{targetIds.Count} tài khoản thành công";
                XmapStatus = $"Đã tới {target.DisplayText}";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Đã dừng Xmap.";
            XmapStatus = "Đã dừng";
        }
        finally
        {
            if (_xmapCts == cts)
            {
                IsNavigatingXmap = false;
                _xmapCts = null;
            }
            cts.Dispose();
        }
    }

    [RelayCommand]
    public void StopXmap()
    {
        _xmapCts?.Cancel();
        IsNavigatingXmap = false;
        XmapStatus = "Đã dừng";
        StatusText = "Đã dừng Xmap.";
    }

    [RelayCommand]
    public void ReloadXmap()
    {
        WorldMapGraph.Instance.Initialize();
        InitializeXmapData();
        StatusText = "Đã nạp lại dữ liệu map.";
    }

    [RelayCommand]
    public void SelectConnectedNode(object? parameter)
    {
        int targetId = ResolveTargetMapId(parameter);
        if (targetId >= 0)
        {
            var target = _allXmapMaps.FirstOrDefault(m => m.Id == targetId);
            if (target != null)
            {
                SelectedXmapTarget = target;
            }
        }
    }

    [RelayCommand]
    public void OpenMapGraphDialog()
    {
        var vm = new XmapNodePickerViewModel(CurrentMapId, CurrentMapName, SelectedXmapTarget?.Id);
        var dialog = new XmapNodePickerDialog(vm)
        {
            Owner = Application.Current?.MainWindow
        };

        bool? result = dialog.ShowDialog();
        if (result == true && dialog.SelectedNode != null)
        {
            var found = _allXmapMaps.FirstOrDefault(m => m.Id == dialog.SelectedNode.Id);
            if (found != null)
            {
                SelectedXmapTarget = found;
            }
        }
    }

    [RelayCommand]
    public void InspectNode(object? parameter)
    {
        int targetId = ResolveTargetMapId(parameter);
        if (targetId >= 0)
        {
            SetFocalMap(targetId, isUserExploration: true);
        }
    }

    [RelayCommand]
    public void ResetFocalToCurrent()
    {
        if (CurrentMapId >= 0)
        {
            SetFocalMap(CurrentMapId, isUserExploration: false);
        }
    }

    [RelayCommand]
    public async Task NavigateDirectNode(object? parameter)
    {
        int targetId = ResolveTargetMapId(parameter);
        if (targetId < 0) return;

        var targetMap = _allXmapMaps.FirstOrDefault(m => m.Id == targetId);
        if (targetMap != null)
        {
            SelectedXmapTarget = targetMap;
            await StartXmap(targetMap);
        }
    }

    [RelayCommand]
    public void SelectQuickTargetMapId()
    {
        if (int.TryParse(QuickTargetMapIdInput.Trim(), out int targetId))
        {
            var target = _allXmapMaps.FirstOrDefault(m => m.Id == targetId);
            if (target != null)
            {
                SelectedXmapTarget = target;
                QuickTargetMapIdInput = string.Empty;
            }
            else
            {
                StatusText = $"Không tìm thấy Map #{targetId}";
            }
        }
    }
}
