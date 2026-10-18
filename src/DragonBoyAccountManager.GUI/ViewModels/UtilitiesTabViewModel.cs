using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Game.Entities;
using DragonBoyAccountManager.Core.Game.Models;
using DragonBoyAccountManager.Core.Game.Modules;
using DragonBoyAccountManager.Core.Game.Snapshots;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.GameEngine;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed record WaypointButtonItem(
    string Name,
    int X,
    int Y,
    WorldWaypointInfo Waypoint
);

public sealed partial class UtilitiesTabViewModel : ObservableObject
{
    private readonly IGameEngineService _gameEngine;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _refreshTimer;
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

    [ObservableProperty]
    private string _chatText = "Chào mọi người!";

    [ObservableProperty]
    private int _chatDelaySeconds = 5;

    [ObservableProperty]
    private int _selectedChatTypeIndex = 0;

    [ObservableProperty]
    private bool _isRandomChat;

    [ObservableProperty]
    private bool _autoChatSyncAll = true;

    [ObservableProperty]
    private bool _isAutoChatRunning;

    [ObservableProperty]
    private int _chatSentCount;

    [ObservableProperty]
    private string _autoChatStatusText = "Chưa kích hoạt";

    [ObservableProperty]
    private string _lastChatSentTime = string.Empty;

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

    public UtilitiesTabViewModel(IGameEngineService gameEngine, ISettingsService settingsService)
    {
        _gameEngine = gameEngine;
        _settingsService = settingsService;

        _fastTeleport = _settingsService.Current.FastTeleport;

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        _refreshTimer.Tick += (_, _) => RefreshMapData();

        _gameEngine.OnChatSent += HandleChatSent;
    }

    public void StartPolling()
    {
        if (!_refreshTimer.IsEnabled)
        {
            _refreshTimer.Start();
        }
    }

    public void StopPolling()
    {
        if (_refreshTimer.IsEnabled)
        {
            _refreshTimer.Stop();
        }
    }

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
            if (!AutoChatSyncAll)
            {
                IsAutoChatRunning = false;
            }
        }
        else
        {
            if (!AutoChatSyncAll)
            {
                IsAutoChatRunning = _gameEngine.IsAutoChatRunning(account.Id);
            }
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
                StatusText = $"Đã chuyển tới {wp.Name} ({targetIds.Count} acc)";
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
        StatusText = $"Đã chọn {entityName} ({targetIds.Count} acc)";
    }

    public void UpdateSyncTargetDisplay()
    {
        var targets = GetTargetAccountIds();
        int count = targets.Count;
        SyncTargetDisplay = count > 1 ? $"Đồng bộ: {count} acc" : (count == 1 ? "1 tài khoản" : "Chưa chọn acc");
    }

    [RelayCommand]
    public void UseCapsule()
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
            var res = _gameEngine.UseCapsule(id);
            if (res.Success)
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
            StatusText = $"Dùng Capsule: {successCount}/{targets.Count} acc thành công";
        }
    }

    [RelayCommand]
    public void UseTdlt()
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
            var res = _gameEngine.UseTdlt(id);
            if (res.Success)
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
            StatusText = $"Dùng TDLT: {successCount}/{targets.Count} acc thành công";
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

        StatusText = $"Đã đặt tốc độ {value:0.0}x ({targets.Count} acc)";
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
        if (IsApplySpeedToAll && _speedTargetResolver != null)
        {
            var all = _speedTargetResolver();
            if (all.Count > 0) return all;
        }

        return GetTargetAccountIds();
    }

    #region Auto Chat Logic

    [RelayCommand]
    private void SetChatDelay(object? parameter)
    {
        if (parameter is int sec)
        {
            ChatDelaySeconds = Math.Max(1, sec);
        }
        else if (parameter != null && int.TryParse(parameter.ToString(), out int parsed))
        {
            ChatDelaySeconds = Math.Max(1, parsed);
        }
    }

    [RelayCommand]
    private void ToggleAutoChat()
    {
        if (IsAutoChatRunning)
        {
            StopAutoChat();
        }
        else
        {
            StartAutoChat();
        }
    }

    [RelayCommand]
    private async Task SendChatOnce()
    {
        if (string.IsNullOrWhiteSpace(ChatText))
        {
            StatusText = "Nội dung chat trống!";
            return;
        }

        var lines = ChatText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length == 0) return;

        string msg = lines[0];
        var targets = AutoChatSyncAll ? GetTargetAccountIds() : (ActiveAccount != null && ActiveAccount.IsRunning ? [ActiveAccount.Id] : []);
        var chatType = (ChatType)SelectedChatTypeIndex;

        if (targets.Count == 0)
        {
            StatusText = "Không có acc nào trong game để gửi!";
            return;
        }

        int count = 0;
        foreach (var id in targets)
        {
            if (await _gameEngine.SendChatAsync(id, msg, chatType)) count++;
        }

        StatusText = count > 0 ? $"Đã gửi 1 tin chat tới {count} acc lúc {DateTime.Now:HH:mm:ss}" : "Không thể gửi tin nhắn chat!";
    }

    private void StartAutoChat()
    {
        if (string.IsNullOrWhiteSpace(ChatText))
        {
            StatusText = "Vui lòng nhập nội dung chat trước!";
            return;
        }

        var lines = ChatText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToArray();

        if (lines.Length == 0)
        {
            StatusText = "Vui lòng nhập nội dung chat trước!";
            return;
        }

        var targets = AutoChatSyncAll ? GetTargetAccountIds() : (ActiveAccount != null && ActiveAccount.IsRunning ? [ActiveAccount.Id] : []);
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            return;
        }

        var chatType = (ChatType)SelectedChatTypeIndex;
        var interval = TimeSpan.FromSeconds(Math.Max(1, ChatDelaySeconds));

        ChatSentCount = 0;
        IsAutoChatRunning = true;
        AutoChatStatusText = $"Đang chạy auto chat ({targets.Count} acc)...";

        foreach (var id in targets)
        {
            _gameEngine.StartAutoChat(id, lines, interval, chatType, IsRandomChat);
        }
    }

    private void StopAutoChat()
    {
        var targets = AutoChatSyncAll ? GetTargetAccountIds() : (ActiveAccount != null && ActiveAccount.IsRunning ? [ActiveAccount.Id] : []);
        foreach (var id in targets)
        {
            _gameEngine.StopAutoChat(id);
        }
        IsAutoChatRunning = false;
        AutoChatStatusText = $"Đã dừng (Đã gửi tổng cộng: {ChatSentCount} tin)";
    }

    private void HandleChatSent(Guid accountId, string message, int count)
    {
        var targets = AutoChatSyncAll ? GetTargetAccountIds() : (ActiveAccount != null ? [ActiveAccount.Id] : []);
        if (!targets.Contains(accountId)) return;

        var dispatcher = App.Current?.Dispatcher;
        if (dispatcher == null) return;

        _ = dispatcher.InvokeAsync(() =>
        {
            ChatSentCount++;
            LastChatSentTime = DateTime.Now.ToString("HH:mm:ss");
            AutoChatStatusText = $"Đang chạy... Đã gửi: {ChatSentCount} tin ({LastChatSentTime})";
        });
    }

    partial void OnAutoChatSyncAllChanged(bool value)
    {
        if (value)
        {
            IsAutoChatRunning = GetTargetAccountIds().Any(id => _gameEngine.IsAutoChatRunning(id));
        }
        else
        {
            IsAutoChatRunning = ActiveAccount != null && _gameEngine.IsAutoChatRunning(ActiveAccount.Id);
        }
    }

    #endregion
}
