using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.DesignSystem.Assists;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts;
using DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Items;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities;

public sealed partial class MapUtilitiesTabViewModel : AccountAwareTabViewModel
{
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private bool _fastTeleport;

    partial void OnFastTeleportChanged(bool value)
    {
        _settingsService.Current.FastTeleport = value;
        _ = _settingsService.SaveAsync();
    }

    [ObservableProperty]
    private string _currentMapName = "Chưa vào game";

    [ObservableProperty]
    private int _currentMapId = -1;

    [ObservableProperty]
    private int _currentZoneId = -1;

    [ObservableProperty]
    private int _currentPlayers;

    [ObservableProperty]
    private int _maxPlayers = 15;

    [ObservableProperty]
    private string _zonePlayerDisplay = "0/15";

    [ObservableProperty]
    private string _zoneInput = "0";

    [ObservableProperty]
    private int _totalMobs;

    [ObservableProperty]
    private int _aliveMobs;

    [ObservableProperty]
    private int _bossCount;

    [ObservableProperty]
    private int _playerCount;

    [ObservableProperty]
    private int _waypointCount;

    [ObservableProperty]
    private int _npcCount;

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

    private CancellationTokenSource? _moveWaypointCts;
    private CancellationTokenSource? _xmapCts;

    public ObservableCollection<WaypointButtonItem> WaypointButtons { get; } = [];
    public ObservableCollection<WorldBossInfo> Bosses { get; } = [];
    public ObservableCollection<WorldPlayerInfo> Players { get; } = [];
    public ObservableCollection<WorldMobInfo> Mobs { get; } = [];
    public ObservableCollection<WorldWaypointInfo> Waypoints { get; } = [];
    public ObservableCollection<WorldNpcInfo> Npcs { get; } = [];
    public ObservableCollection<WorldZoneInfo> AvailableZones { get; } = [];
    public ObservableCollection<int> QuickZones { get; } = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

    public ObservableCollection<XmapMapItemViewModel> FilteredXmapMaps { get; } = [];
    public ObservableCollection<string> PlanetFilters { get; } = ["Tất cả", .. PlanetNames.All];
    private readonly List<XmapMapItemViewModel> _allXmapMaps = [];

    private bool _isRefreshingZones;
    private WorldZoneInfo? _selectedZone;
    public WorldZoneInfo? SelectedZone
    {
        get => _selectedZone;
        set
        {
            if (SetProperty(ref _selectedZone, value))
            {
                if (value != null && !_isRefreshingZones)
                {
                    ExecuteChangeZone(value.ZoneId);
                }
            }
        }
    }

    public MapUtilitiesTabViewModel(
        IGameEngineService gameEngine,
        ISettingsService settingsService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, selectionService, dispatcher)
    {
        _settingsService = settingsService;
        _fastTeleport = _settingsService.Current.FastTeleport;
        InitializeXmapData();
    }

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
                if (IsFocalAtCurrentMap && liveWps != null && t.Npc == null)
                {
                    dir = WorldMapGraph.Instance.ResolveDirection(mapId, t.ToMapId, false, liveWps);
                }

                string dirLabel = dir switch
                {
                    MapDirection.Left => "Cổng Trái",
                    MapDirection.Right => "Cổng Phải",
                    MapDirection.Center => "Cổng Giữa",
                    MapDirection.Npc => "NPC",
                    _ => "Cổng"
                };

                string coord = string.Empty;
                if (IsFocalAtCurrentMap && liveWps != null)
                {
                    var matchingWp = liveWps.FirstOrDefault(w =>
                        !string.IsNullOrWhiteSpace(w.Name) &&
                        (w.Name == t.ToMapName || WorldMapData.MatchMapName(w.Name, t.ToMapName)));
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

    public override void OnTabActivated()
    {
        RefreshMapData();
        UpdateSyncTargetDisplay();
    }

    protected override void OnActiveSnapshotUpdated(GameSessionSnapshot snapshot)
    {
        ApplyWorldSnapshot(snapshot.World);
    }

    protected override void OnActiveAccountOffline()
    {
        CurrentMapName = "Tài khoản đang tắt";
        ClearData();
    }

    public void RequestZoneList()
    {
        if (ActiveAccount != null && ActiveAccount.IsRunning && ActiveAccount.Status == GameSessionStatus.Active)
        {
            _gameEngine.RequestZoneList(ActiveAccount.Id);
            _ = Task.Run(async () =>
            {
                await Task.Delay(350, CancellationToken.None);
                RunOnUi(RefreshMapData);
            });
        }
    }

    public override void SetActiveAccount(AccountItemViewModel? account)
    {
        ActiveAccount = account;
        HasActiveAccount = account != null;
        IsAccountRunning = account?.IsRunning == true;

        if (account == null)
        {
            ActiveAccountInfo = "Chưa chọn tài khoản";
            CurrentMapName = "Chưa chọn tài khoản";
            CurrentMapId = -1;
            CurrentZoneId = -1;
            CurrentPlayers = 0;
            MaxPlayers = 15;
            ZonePlayerDisplay = "0/15";
            ClearData();
        }
        else
        {
            ActiveAccountInfo = FormatAccountDisplayInfo(account);
            RefreshMapData();
        }
    }

    [RelayCommand]
    public void Refresh()
    {
        RefreshMapData();
    }

    public void RefreshMapData()
    {
        if (ActiveAccount == null || !ActiveAccount.IsRunning)
        {
            IsAccountRunning = false;
            CurrentMapName = ActiveAccount == null ? "Chưa chọn tài khoản" : "Tài khoản đang tắt";
            CurrentMapId = -1;
            CurrentZoneId = -1;
            CurrentPlayers = 0;
            MaxPlayers = 15;
            ZonePlayerDisplay = "0/15";
            ClearData();
            return;
        }

        if (ActiveAccount.Status != GameSessionStatus.Active)
        {
            IsAccountRunning = true;
            CurrentMapName = "Đang đăng nhập / chưa vào map...";
            CurrentMapId = -1;
            CurrentZoneId = -1;
            CurrentPlayers = 0;
            MaxPlayers = 15;
            ZonePlayerDisplay = "0/15";
            ClearData();
            return;
        }

        IsAccountRunning = true;
        var snapshot = _gameEngine.GetWorldSnapshot(ActiveAccount.Id);
        if (snapshot != null)
        {
            ApplyWorldSnapshot(snapshot);
        }
    }

    public void ApplyWorldSnapshot(WorldSnapshot snapshot)
    {
        if (snapshot == null || snapshot.MapId < 0)
        {
            CurrentMapName = "Đang tải dữ liệu bản đồ...";
            return;
        }

        int previousMapId = CurrentMapId;
        CurrentMapId = snapshot.MapId;
        CurrentZoneId = snapshot.ZoneId;
        CurrentMapName = !string.IsNullOrWhiteSpace(snapshot.MapName)
            ? snapshot.MapName
            : $"Map #{snapshot.MapId}";

        CurrentPlayers = snapshot.CurrentPlayers;
        MaxPlayers = snapshot.MaxPlayers;
        ZonePlayerDisplay = $"{CurrentPlayers}/{MaxPlayers}";

        TotalMobs = snapshot.TotalMobs;
        AliveMobs = snapshot.AliveMobs;
        BossCount = snapshot.Bosses.Count;
        PlayerCount = snapshot.Players.Count;
        WaypointCount = snapshot.Waypoints.Count;
        NpcCount = snapshot.Npcs.Count;

        Bosses.SyncWith(snapshot.Bosses);
        Players.SyncWith(snapshot.Players);
        Mobs.SyncWith(snapshot.Mobs);
        Waypoints.SyncWith(snapshot.Waypoints);
        Npcs.SyncWith(snapshot.Npcs);

        AvailableZones.SyncZones(snapshot.Zones, snapshot.ZoneId, ref _selectedZone, ref _isRefreshingZones, () => OnPropertyChanged(nameof(SelectedZone)));

        var sortedWps = snapshot.Waypoints.OrderBy(w => w.MinX).ToList();
        var newWaypointButtons = sortedWps.Take(4)
            .Select((wp, idx) =>
            {
                string displayName = !string.IsNullOrWhiteSpace(wp.Name)
                    ? wp.Name
                    : (sortedWps.Count switch
                    {
                        1 => "Cổng Vực",
                        2 => idx == 0 ? "Cổng Trái" : "Cổng Phải",
                        3 => idx == 0 ? "Cổng Trái" : (idx == 1 ? "Cổng Giữa" : "Cổng Phải"),
                        _ => $"Cổng {idx + 1}"
                    });
                return new WaypointButtonItem(displayName, wp.X, wp.Y, wp);
            })
            .ToList();
        WaypointButtons.SyncWith(newWaypointButtons);

        UpdateCurrentMapFlags();

        if (IsFocalAtCurrentMap || FocalMapId < 0 || previousMapId != CurrentMapId)
        {
            SetFocalMap(CurrentMapId, isUserExploration: false);
        }

        UpdateSyncTargetDisplay();
        StatusText = $"Cập nhật lúc: {DateTime.Now:HH:mm:ss}";
    }

    private void ClearData()
    {
        TotalMobs = 0;
        AliveMobs = 0;
        BossCount = 0;
        PlayerCount = 0;
        WaypointCount = 0;
        NpcCount = 0;
        WaypointButtons.Clear();
        Bosses.Clear();
        Players.Clear();
        Mobs.Clear();
        Waypoints.Clear();
        Npcs.Clear();
        _isRefreshingZones = true;
        try
        {
            AvailableZones.Clear();
            _selectedZone = null;
            OnPropertyChanged(nameof(SelectedZone));
        }
        finally
        {
            _isRefreshingZones = false;
        }
        CurrentMapId = -1;
        FocalMapId = -1;
        FocalMapName = "Chưa chọn bản đồ";
        FocalPlanetName = string.Empty;
        FocalMapCoordinates = string.Empty;
        AllAdjacentNodes.Clear();
        UpdateCurrentMapFlags();
        StatusText = string.Empty;
    }
}
