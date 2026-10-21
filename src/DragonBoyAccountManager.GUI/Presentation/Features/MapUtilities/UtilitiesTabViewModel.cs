using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class UtilitiesTabViewModel : ObservableObject
{
    private readonly IGameEngineService _gameEngine;
    private readonly ISettingsService _settingsService;
    private readonly IActiveSelectionService? _selectionService;
    private Func<List<Guid>>? _targetResolver;

    [ObservableProperty]
    private bool _fastTeleport;

    partial void OnFastTeleportChanged(bool value)
    {
        _settingsService.Current.FastTeleport = value;
        _ = _settingsService.SaveAsync();
    }

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameSpeedText))]
    private float _gameSpeed = 1.0f;

    [ObservableProperty]
    private bool _isApplySpeedToAll = true;

    public string GameSpeedText => $"{GameSpeed:0.0}x";

    private bool _isInternalSpeedUpdate;
    private Func<List<Guid>>? _speedTargetResolver;

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
        IActiveSelectionService? selectionService = null)
    {
        _gameEngine = gameEngine;
        _settingsService = settingsService;
        _selectionService = selectionService;

        if (_selectionService != null)
        {
            _selectionService.SelectedAccountChanged += SetActiveAccount;
            _selectionService.TargetsChanged += UpdateSyncTargetDisplay;
        }

        _fastTeleport = _settingsService.Current.FastTeleport;

        WeakReferenceMessenger.Default.Register<SessionSnapshotUpdatedMessage>(this, (_, m) =>
        {
            if (ActiveAccount?.Id == m.AccountId)
            {
                var dispatcher = App.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    _ = dispatcher.InvokeAsync(() =>
                    {
                        if (m.Status == GameSessionStatus.Active && m.Snapshot != null)
                        {
                            IsAccountRunning = true;
                            ApplyWorldSnapshot(m.Snapshot.World);
                        }
                        else if (m.Status == GameSessionStatus.Offline)
                        {
                            IsAccountRunning = false;
                            CurrentMapName = "Tài khoản đang tắt";
                            ClearData();
                        }
                    });
                }
            }
        });

        WeakReferenceMessenger.Default.Register<AccountSelectedMessage>(this, (_, m) =>
        {
            SetActiveAccount(m.Account);
            UpdateSyncTargetDisplay();
        });

        WeakReferenceMessenger.Default.Register<TargetAccountsChangedMessage>(this, (_, _) =>
        {
            UpdateSyncTargetDisplay();
        });
    }

    public void StartPolling() { }

    public void StopPolling() { }

    public void SetTargetResolver(Func<List<Guid>> resolver)
    {
        _targetResolver = resolver;
    }

    public void SetSpeedTargetResolver(Func<List<Guid>> resolver)
    {
        _speedTargetResolver = resolver;
    }

    public void RequestZoneList()
    {
        if (ActiveAccount != null && ActiveAccount.IsRunning && ActiveAccount.Status == GameSessionStatus.Active)
        {
            _gameEngine.RequestZoneList(ActiveAccount.Id);
            _ = Task.Run(async () =>
            {
                await Task.Delay(350);
                var dispatcher = App.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(RefreshMapData);
                }
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

        UpdateCollection(Bosses, snapshot.Bosses);
        UpdateCollection(Players, snapshot.Players);
        UpdateCollection(Mobs, snapshot.Mobs);
        UpdateCollection(Waypoints, snapshot.Waypoints);
        UpdateCollection(Npcs, snapshot.Npcs);

        UpdateZones(snapshot.Zones, snapshot.ZoneId);

        var sortedWps = snapshot.Waypoints.OrderBy(w => w.X).ToList();
        var newWaypointButtons = sortedWps.Take(3)
            .Select(wp => new WaypointButtonItem(wp.Name, wp.X, wp.Y, wp))
            .ToList();
        UpdateCollection(WaypointButtons, newWaypointButtons);

        UpdateSyncTargetDisplay();
        StatusText = $"Cập nhật lúc: {DateTime.Now:HH:mm:ss}";
    }

    private void UpdateZones(IReadOnlyList<WorldZoneInfo> newZones, int currentZoneId)
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
        if (target.Count == source.Count && target.SequenceEqual(source))
        {
            return;
        }

        int commonCount = Math.Min(target.Count, source.Count);
        for (int i = 0; i < commonCount; i++)
        {
            if (!EqualityComparer<T>.Default.Equals(target[i], source[i]))
            {
                target[i] = source[i];
            }
        }

        if (target.Count > source.Count)
        {
            for (int i = target.Count - 1; i >= source.Count; i--)
            {
                target.RemoveAt(i);
            }
        }
        else if (target.Count < source.Count)
        {
            for (int i = target.Count; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
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
        if (_selectionService != null)
        {
            var running = _selectionService.GetRunningTargetAccountIds();
            if (running.Count > 0) return running.ToList();
        }

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

        StatusText = $"Đã chuyển sang Khu {targetZone} ({successCount}/{targetIds.Count} tài khoản)";
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            var dispatcher = App.Current?.Dispatcher;
            if (dispatcher != null)
            {
                await dispatcher.InvokeAsync(RefreshMapData);
            }
        });
    }

    [RelayCommand]
    public void NextZone()
    {
        int next = (CurrentZoneId < 0 ? 0 : CurrentZoneId) + 1;
        ExecuteChangeZone(next);
    }

    [RelayCommand]
    public void PreviousZone()
    {
        int prev = (CurrentZoneId < 0 ? 0 : CurrentZoneId) - 1;
        if (prev < 0) prev = 0;
        ExecuteChangeZone(prev);
    }

    [RelayCommand]
    public void ChangeZone(object? parameter)
    {
        int targetZone = -1;

        if (parameter is int zInt)
        {
            targetZone = zInt;
        }
        else if (parameter is WorldZoneInfo zoneInfo)
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
    public async Task MoveToWaypoint(object? parameter)
    {
        WorldWaypointInfo? wp = null;
        if (parameter is WaypointButtonItem btnItem)
        {
            wp = btnItem.Waypoint;
        }
        else if (parameter is WorldWaypointInfo wpInfo)
        {
            wp = wpInfo;
        }

        if (wp == null) return;

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0) return;

        _moveWaypointCts?.Cancel();
        _moveWaypointCts?.Dispose();
        var cts = new CancellationTokenSource();
        _moveWaypointCts = cts;

        StatusText = $"Đang di chuyển tới {wp.Name}...";

        bool isTeleport = FastTeleport;

        try
        {
            var tasks = targetIds.Select(async id =>
            {
                int startMapId = _gameEngine.GetWorldSnapshot(id)?.MapId ?? -1;

                await _gameEngine.AutoMoveAsync(id, wp.X, wp.Y, isTeleport, cts.Token);

                if (cts.Token.IsCancellationRequested) return;

                var currentWorld = _gameEngine.GetWorldSnapshot(id);
                var currentMapId = currentWorld?.MapId ?? -1;
                var isChangingMap = _gameEngine.GetPlayerSnapshot(id)?.IsChangingMap ?? false;

                if ((startMapId >= 0 && currentMapId >= 0 && currentMapId != startMapId) || isChangingMap)
                {
                    return;
                }

                await Task.Delay(100, cts.Token);

                if (cts.Token.IsCancellationRequested) return;

                currentWorld = _gameEngine.GetWorldSnapshot(id);
                currentMapId = currentWorld?.MapId ?? -1;
                isChangingMap = _gameEngine.GetPlayerSnapshot(id)?.IsChangingMap ?? false;

                if ((startMapId >= 0 && currentMapId >= 0 && currentMapId != startMapId) || isChangingMap)
                {
                    return;
                }

                _gameEngine.MoveToWaypoint(id, wp.X, wp.Y);
            });

            await Task.WhenAll(tasks);

            if (!cts.Token.IsCancellationRequested)
            {
                StatusText = $"Đã chuyển tới {wp.Name} ({targetIds.Count} tài khoản)";
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand]
    public async Task ClickObject(object? parameter)
    {
        if (parameter == null) return;

        string entityType = string.Empty;
        int entityId = 0;
        string entityName = string.Empty;
        int targetX = 0;
        int targetY = 0;

        if (parameter is WorldMobInfo mob)
        {
            entityType = "mob";
            entityId = mob.Id;
            entityName = mob.Name;
            targetX = mob.X;
            targetY = mob.Y;
        }
        else if (parameter is WorldBossInfo boss)
        {
            entityType = "boss";
            entityId = boss.Id;
            entityName = boss.Name;
            targetX = boss.X;
            targetY = boss.Y;
        }
        else if (parameter is WorldPlayerInfo player)
        {
            entityType = "player";
            entityId = player.Id;
            entityName = player.Name;
            targetX = player.X;
            targetY = player.Y;
        }
        else if (parameter is WorldNpcInfo npc)
        {
            entityType = "npc";
            entityId = npc.Id;
            entityName = npc.Name;
            targetX = npc.X;
            targetY = npc.Y;
        }

        if (string.IsNullOrEmpty(entityType)) return;

        var targetIds = GetTargetAccountIds();
        if (targetIds.Count == 0) return;

        StatusText = $"Đang tiếp cận và chọn {entityName}...";

        bool isTeleport = FastTeleport;

        var tasks = targetIds.Select(async id =>
        {
            if (targetX > 0 && targetY > 0)
            {
                await _gameEngine.AutoMoveAsync(id, targetX, targetY, isTeleport);
            }
            _gameEngine.FocusOrClickObject(id, entityType, entityId);
        });

        await Task.WhenAll(tasks);
        StatusText = $"Đã chọn {entityName} ({targetIds.Count} tài khoản)";
    }

    public void UpdateSyncTargetDisplay()
    {
        var targets = GetTargetAccountIds();
        int count = targets.Count;
        SyncTargetDisplay = count > 1 ? $"Đồng bộ: {count} tài khoản" : (count == 1 ? "1 tài khoản" : "Chưa chọn tài khoản");
    }

    [RelayCommand]
    public async Task UseCapsuleAsync()
    {
        var targets = GetTargetAccountIds();
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        int successCount = 0;
        string lastMsg = string.Empty;
        foreach (var id in targets)
        {
            var res = await _gameEngine.UseCapsuleAsync(id);
            if (res.IsSuccess)
            {
                successCount++;
            }
            lastMsg = res.Message;
        }

        if (targets.Count == 1)
        {
            StatusText = lastMsg;
        }
        else
        {
            StatusText = $"Dùng Capsule: {successCount}/{targets.Count} tài khoản thành công";
        }
    }

    [RelayCommand]
    public async Task UseTdltAsync()
    {
        var targets = GetTargetAccountIds();
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        int successCount = 0;
        string lastMsg = string.Empty;
        foreach (var id in targets)
        {
            var res = await _gameEngine.UseTdltAsync(id);
            if (res.IsSuccess)
            {
                successCount++;
            }
            lastMsg = res.Message;
        }

        if (targets.Count == 1)
        {
            StatusText = lastMsg;
        }
        else
        {
            StatusText = $"Dùng TDLT: {successCount}/{targets.Count} tài khoản thành công";
        }
    }

    partial void OnGameSpeedChanged(float value)
    {
        if (_isInternalSpeedUpdate) return;
        var targets = ResolveSpeedTargets();
        if (targets.Count == 0) return;

        foreach (var id in targets)
        {
            _gameEngine.SetGameSpeed(id, value);
        }

        StatusText = $"Đã đặt tốc độ {value:0.0}x ({targets.Count} tài khoản)";
    }

    [RelayCommand]
    private void SetGameSpeed(string speedStr)
    {
        if (float.TryParse(speedStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float speed))
        {
            GameSpeed = speed;
        }
    }

    private List<Guid> ResolveSpeedTargets()
    {
        if (IsApplySpeedToAll)
        {
            var all = _selectionService?.GetRunningSpeedTargetAccountIds(true).ToList() ?? _speedTargetResolver?.Invoke() ?? GetTargetAccountIds();
            if (all.Count > 0) return all;
        }

        return GetTargetAccountIds();
    }
}
