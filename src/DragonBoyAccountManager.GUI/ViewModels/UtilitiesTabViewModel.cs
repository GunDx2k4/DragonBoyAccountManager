using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Game.Objects;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.GameEngine;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed record WaypointButtonItem(
    string Name,
    int X,
    int Y,
    MapWaypointInfo Waypoint
);

public sealed partial class UtilitiesTabViewModel : ObservableObject
{
    private readonly IGameEngineService _gameEngine;
    private readonly DispatcherTimer _refreshTimer;
    private Func<List<Guid>>? _targetResolver;

    [ObservableProperty]
    private AccountItemViewModel? _activeAccount;

    [ObservableProperty]
    private string _activeAccountInfo = "Chưa chọn tài khoản";

    [ObservableProperty]
    private bool _hasActiveAccount;

    [ObservableProperty]
    private bool _isAccountRunning;

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
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _syncTargetDisplay = "1 tài khoản";

    public ObservableCollection<WaypointButtonItem> WaypointButtons { get; } = [];
    public ObservableCollection<MapBossInfo> Bosses { get; } = [];
    public ObservableCollection<MapPlayerInfo> Players { get; } = [];
    public ObservableCollection<MapMobInfo> Mobs { get; } = [];
    public ObservableCollection<MapWaypointInfo> Waypoints { get; } = [];
    public ObservableCollection<MapNpcInfo> Npcs { get; } = [];
    public ObservableCollection<MapZoneInfo> AvailableZones { get; } = [];
    public ObservableCollection<int> QuickZones { get; } = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];

    private bool _isRefreshingZones;
    private MapZoneInfo? _selectedZone;
    public MapZoneInfo? SelectedZone
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

    public UtilitiesTabViewModel(IGameEngineService gameEngine)
    {
        _gameEngine = gameEngine;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        _refreshTimer.Tick += (_, _) => RefreshMapData();
        _refreshTimer.Start();
    }

    public void SetTargetResolver(Func<List<Guid>> resolver)
    {
        _targetResolver = resolver;
    }

    public void RequestZoneList()
    {
        if (ActiveAccount != null && ActiveAccount.IsRunning && ActiveAccount.Status == GameSessionStatus.Active)
        {
            _gameEngine.RequestZoneList(ActiveAccount.Id);
            Task.Delay(350).ContinueWith(_ =>
            {
                App.Current?.Dispatcher?.Invoke(RefreshMapData);
            });
        }
    }

    public void SetActiveAccount(AccountItemViewModel? account)
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
            string server = !string.IsNullOrWhiteSpace(account.Server) ? $" ({account.Server})" : "";
            string charName = !string.IsNullOrWhiteSpace(account.CharacterName) ? $" [{account.CharacterName}]" : "";
            ActiveAccountInfo = $"{account.Username}{server}{charName}";
            if (account.IsRunning && account.Status == GameSessionStatus.Active)
            {
                _gameEngine.RequestZoneList(account.Id);
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
        var snapshot = _gameEngine.GetMapSnapshot(ActiveAccount.Id);
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

        // Update collections
        UpdateCollection(Bosses, snapshot.Bosses);
        UpdateCollection(Players, snapshot.Players);
        UpdateCollection(Mobs, snapshot.Mobs);
        UpdateCollection(Waypoints, snapshot.Waypoints);
        UpdateCollection(Npcs, snapshot.Npcs);

        // Update Zone Dropdown list (đồng bộ theo openzonemap trong panel code gốc)
        UpdateZones(snapshot.Zones, snapshot.ZoneId);

        // Update Waypoint buttons (tối đa 3 nút, chỉ hiển thị tên đích đến và tọa độ, tự ẩn nếu không có)
        var sortedWps = snapshot.Waypoints.OrderBy(w => w.X).ToList();
        WaypointButtons.Clear();
        foreach (var wp in sortedWps.Take(3))
        {
            WaypointButtons.Add(new WaypointButtonItem(wp.Name, wp.X, wp.Y, wp));
        }

        UpdateSyncTargetDisplay();
        StatusText = $"Cập nhật lúc: {DateTime.Now:HH:mm:ss}";
    }

    private void UpdateZones(IReadOnlyList<MapZoneInfo> newZones, int currentZoneId)
    {
        _isRefreshingZones = true;
        try
        {
            bool needRebuild = AvailableZones.Count != newZones.Count;
            if (!needRebuild)
            {
                for (int i = 0; i < newZones.Count; i++)
                {
                    if (AvailableZones[i].ZoneId != newZones[i].ZoneId)
                    {
                        needRebuild = true;
                        break;
                    }
                }
            }

            if (needRebuild)
            {
                AvailableZones.Clear();
                foreach (var z in newZones)
                {
                    AvailableZones.Add(z);
                }
            }
            else
            {
                for (int i = 0; i < newZones.Count; i++)
                {
                    if (AvailableZones[i] != newZones[i])
                    {
                        AvailableZones[i] = newZones[i];
                    }
                }
            }

            if (SelectedZone?.ZoneId != currentZoneId)
            {
                _selectedZone = AvailableZones.FirstOrDefault(z => z.ZoneId == currentZoneId);
                OnPropertyChanged(nameof(SelectedZone));
            }
        }
        finally
        {
            _isRefreshingZones = false;
        }
    }

    private static void UpdateCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
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

    private List<Guid> GetTargetAccountIds()
    {
        if (_targetResolver != null)
        {
            var list = _targetResolver();
            if (list.Count > 0) return list;
        }

        if (ActiveAccount != null && ActiveAccount.IsRunning)
        {
            return [ActiveAccount.Id];
        }

        return [];
    }

    public void ExecuteChangeZone(int targetZone)
    {
        if (targetZone < 0) return;

        ZoneInput = targetZone.ToString();

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0) return;

        int successCount = 0;
        foreach (var id in targetIds)
        {
            if (_gameEngine.ChangeZone(id, targetZone))
            {
                successCount++;
            }
        }

        StatusText = $"Đã chuyển sang Khu {targetZone} ({successCount}/{targetIds.Count} acc)";
        Task.Delay(1000).ContinueWith(_ =>
        {
            App.Current?.Dispatcher?.Invoke(RefreshMapData);
        });
    }

    [RelayCommand]
    public void ChangeZone(object? parameter)
    {
        int targetZone = -1;

        if (parameter is int zInt)
        {
            targetZone = zInt;
        }
        else if (parameter is MapZoneInfo zoneInfo)
        {
            targetZone = zoneInfo.ZoneId;
        }
        else if (parameter is string zStr && int.TryParse(zStr, out int parsed))
        {
            targetZone = parsed;
        }
        else if (int.TryParse(ZoneInput, out int inputZone))
        {
            targetZone = inputZone;
        }

        if (targetZone >= 0)
        {
            ExecuteChangeZone(targetZone);
        }
    }

    [RelayCommand]
    public void MoveToWaypoint(object? parameter)
    {
        MapWaypointInfo? wp = null;
        if (parameter is WaypointButtonItem btnItem)
        {
            wp = btnItem.Waypoint;
        }
        else if (parameter is MapWaypointInfo wpInfo)
        {
            wp = wpInfo;
        }

        if (wp == null) return;

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0) return;

        int successCount = 0;
        foreach (var id in targetIds)
        {
            if (_gameEngine.MoveToWaypoint(id, wp.X, wp.Y))
            {
                successCount++;
            }
        }

        StatusText = $"Đã di chuyển tới {wp.Name} ({successCount}/{targetIds.Count} acc)";
    }

    [RelayCommand]
    public void ClickObject(object? parameter)
    {
        if (parameter == null) return;

        string entityType = string.Empty;
        int entityId = 0;
        string entityName = string.Empty;

        if (parameter is MapMobInfo mob)
        {
            entityType = "mob";
            entityId = mob.Id;
            entityName = mob.Name;
        }
        else if (parameter is MapBossInfo boss)
        {
            entityType = "boss";
            entityId = boss.Id;
            entityName = boss.Name;
        }
        else if (parameter is MapPlayerInfo player)
        {
            entityType = "player";
            entityId = player.Id;
            entityName = player.Name;
        }
        else if (parameter is MapNpcInfo npc)
        {
            entityType = "npc";
            entityId = npc.Id;
            entityName = npc.Name;
        }

        if (string.IsNullOrEmpty(entityType)) return;

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0) return;

        int successCount = 0;
        foreach (var id in targetIds)
        {
            if (_gameEngine.FocusOrClickObject(id, entityType, entityId))
            {
                successCount++;
            }
        }

        StatusText = $"Đã click vào {entityName} ({successCount}/{targetIds.Count} acc)";
    }

    public void UpdateSyncTargetDisplay()
    {
        var targets = GetTargetAccountIds();
        int count = targets.Count;
        SyncTargetDisplay = count > 1 ? $"Đồng bộ: {count} acc" : (count == 1 ? "1 tài khoản" : "Chưa chọn acc");
    }
}
