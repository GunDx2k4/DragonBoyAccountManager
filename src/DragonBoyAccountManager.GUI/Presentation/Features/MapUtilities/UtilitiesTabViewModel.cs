using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.DesignSystem.Assists;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities;

public sealed partial class UtilitiesTabViewModel : AccountAwareTabViewModel
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
    [NotifyPropertyChangedFor(nameof(GameSpeedText))]
    private float _gameSpeed = 1.0f;

    [ObservableProperty]
    private bool _isApplySpeedToAll = true;

    public string GameSpeedText => $"{GameSpeed:0.0}x";

    private bool _isInternalSpeedUpdate;
    private CancellationTokenSource? _moveWaypointCts;

    public ObservableCollection<WaypointButtonItem> WaypointButtons { get; } = [];
    public ObservableCollection<WorldBossInfo> Bosses { get; } = [];
    public ObservableCollection<WorldPlayerInfo> Players { get; } = [];
    public ObservableCollection<WorldMobInfo> Mobs { get; } = [];
    public ObservableCollection<WorldWaypointInfo> Waypoints { get; } = [];
    public ObservableCollection<WorldNpcInfo> Npcs { get; } = [];
    public ObservableCollection<WorldZoneInfo> AvailableZones { get; } = [];
    public ObservableCollection<int> QuickZones { get; } = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

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

    public UtilitiesTabViewModel(
        IGameEngineService gameEngine,
        ISettingsService settingsService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, selectionService, dispatcher)
    {
        _settingsService = settingsService;
        _fastTeleport = _settingsService.Current.FastTeleport;
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
            if (account.IsRunning && account.Status == GameSessionStatus.Active)
            {
                _gameEngine.RequestZoneList(account.Id);
                float curSpeed = _gameEngine.GetGameSpeed(account.Id);
                if (curSpeed > 0 && Math.Abs(GameSpeed - curSpeed) > 0.05f)
                {
                    _isInternalSpeedUpdate = true;
                    try
                    {
                        GameSpeed = curSpeed;
                    }
                    finally
                    {
                        _isInternalSpeedUpdate = false;
                    }
                }
            }
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

        var sortedWps = snapshot.Waypoints.OrderBy(w => w.X).ToList();
        var newWaypointButtons = sortedWps.Take(3)
            .Select(wp => new WaypointButtonItem(wp.Name, wp.X, wp.Y, wp))
            .ToList();
        WaypointButtons.SyncWith(newWaypointButtons);

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
        StatusText = string.Empty;
    }
}
