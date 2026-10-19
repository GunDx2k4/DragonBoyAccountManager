using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Game.Actions;
using DragonBoyAccountManager.Core.Game.Modules;
using DragonBoyAccountManager.Core.Game.Snapshots;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.GameEngine;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AutoTabViewModel : ObservableObject
{
    private readonly IGameEngineService _gameEngine;
    private readonly DispatcherTimer _refreshTimer;
    private Func<List<Guid>>? _targetResolver;

    [ObservableProperty]
    private AccountItemViewModel? _activeAccount;

    [ObservableProperty]
    private bool _hasActiveAccount;

    [ObservableProperty]
    private bool _isAccountRunning;

    [ObservableProperty]
    private string _activeAccountInfo = "Chưa chọn tài khoản";

    [ObservableProperty]
    private string _syncTargetDisplay = "1 tài khoản";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isAutoTrainRunning;

    [ObservableProperty]
    private string _autoTrainStatusText = "Chưa kích hoạt";

    [ObservableProperty]
    private bool _autoTrainSyncAll = true;

    [ObservableProperty]
    private bool _ignoreBosses = true;

    [ObservableProperty]
    private bool _avoidSuperMobs = true;

    [ObservableProperty]
    private bool _autoPickItems = true;

    [ObservableProperty]
    private bool _autoUsePean = true;

    [ObservableProperty]
    private bool _useTdlt = true;

    [ObservableProperty]
    private int _mobTargetModeIndex = 0;

    [ObservableProperty]
    private string _mobSearchText = string.Empty;

    [ObservableProperty]
    private string _customMobIdInput = string.Empty;

    public ObservableCollection<MobFilterItem> AvailableMobTypes { get; } = [];
    public ICollectionView FilteredMobTypes { get; }
    public ObservableCollection<SkillButtonItem> AutoTrainSkills { get; } = [];

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

    public AutoTabViewModel(IGameEngineService gameEngine)
    {
        _gameEngine = gameEngine;

        FilteredMobTypes = CollectionViewSource.GetDefaultView(AvailableMobTypes);
        FilteredMobTypes.Filter = FilterMobItem;

        for (int i = 0; i < 10; i++)
        {
            AutoTrainSkills.Add(new SkillButtonItem(i, i == 9 ? "Skill 0" : $"Skill {i + 1}", i == 0));
        }

        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1.5)
        };
        _refreshTimer.Tick += (_, _) => RefreshData();

        _gameEngine.OnChatSent += HandleChatSent;
        _gameEngine.OnAutoTrainStatusChanged += HandleAutoTrainStatusChanged;
        _gameEngine.OnAutoTrainStateChanged += HandleAutoTrainStateChanged;
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

    public void SetActiveAccount(AccountItemViewModel? account)
    {
        ActiveAccount = account;
        HasActiveAccount = account != null;
        IsAccountRunning = account?.IsRunning == true;

        if (account == null)
        {
            ActiveAccountInfo = "Chưa chọn tài khoản";
            AvailableMobTypes.Clear();
            if (!AutoChatSyncAll)
            {
                IsAutoChatRunning = false;
            }
            if (!AutoTrainSyncAll)
            {
                IsAutoTrainRunning = false;
            }
        }
        else
        {
            if (!AutoChatSyncAll)
            {
                IsAutoChatRunning = _gameEngine.IsAutoChatRunning(account.Id);
            }
            if (!AutoTrainSyncAll)
            {
                IsAutoTrainRunning = _gameEngine.IsAutoTrainRunning(account.Id);
            }
            string server = !string.IsNullOrWhiteSpace(account.Server) ? $" ({account.Server})" : "";
            string charName = !string.IsNullOrWhiteSpace(account.CharacterName) ? $" [{account.CharacterName}]" : "";
            ActiveAccountInfo = $"{account.Username}{server}{charName}";
            RefreshAutoTrainSkills();
            RefreshData();
        }
    }

    public void RefreshData()
    {
        if (ActiveAccount == null || !ActiveAccount.IsRunning)
        {
            IsAccountRunning = false;
            AvailableMobTypes.Clear();
            FilteredMobTypes.Refresh();
            return;
        }

        if (ActiveAccount.Status != GameSessionStatus.Active)
        {
            IsAccountRunning = true;
            AvailableMobTypes.Clear();
            FilteredMobTypes.Refresh();
            return;
        }

        IsAccountRunning = true;
        var snapshot = _gameEngine.GetWorldSnapshot(ActiveAccount.Id);
        if (snapshot != null && snapshot.Mobs.Count > 0)
        {
            UpdateAvailableMobTypes(snapshot.Mobs);
        }

        RefreshAutoTrainSkills();
        UpdateSyncTargetDisplay();
    }

    partial void OnMobSearchTextChanged(string value)
    {
        FilteredMobTypes.Refresh();
    }

    partial void OnMobTargetModeIndexChanged(int value)
    {
        RefreshMobList();
    }

    private void RefreshMobList()
    {
        if (ActiveAccount == null || !ActiveAccount.IsRunning) return;
        var snapshot = _gameEngine.GetWorldSnapshot(ActiveAccount.Id);
        if (snapshot != null && snapshot.Mobs.Count > 0)
        {
            UpdateAvailableMobTypes(snapshot.Mobs);
        }
        else
        {
            AvailableMobTypes.Clear();
        }
        FilteredMobTypes.Refresh();
    }

    private bool FilterMobItem(object obj)
    {
        if (obj is not MobFilterItem item) return false;
        if (string.IsNullOrWhiteSpace(MobSearchText)) return true;
        string query = MobSearchText.Trim();
        if (item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        if (item.MobId.HasValue && item.MobId.Value.ToString().Contains(query, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void UpdateAvailableMobTypes(IReadOnlyList<WorldMobInfo> mobs)
    {
        if (MobTargetModeIndex == 0)
        {
            var grouped = mobs
                .GroupBy(m => (m.TemplateId, m.Name, m.IsFlying))
                .Select(g => new
                {
                    g.Key.TemplateId,
                    g.Key.Name,
                    g.Key.IsFlying,
                    Hp = g.Max(m => m.Hp),
                    MaxHp = g.Max(m => m.MaxHp),
                    IsSuperMob = g.Any(m => m.IsSuperMob),
                    Count = g.Count()
                })
                .OrderBy(g => g.TemplateId)
                .ToList();

            var existingMap = AvailableMobTypes
                .Where(m => !m.MobId.HasValue)
                .ToDictionary(m => m.TemplateId);
            var currentTemplateIds = grouped.Select(g => g.TemplateId).ToHashSet();

            for (int i = AvailableMobTypes.Count - 1; i >= 0; i--)
            {
                var item = AvailableMobTypes[i];
                if (item.IsCustom) continue;
                if (item.MobId.HasValue || !currentTemplateIds.Contains(item.TemplateId))
                {
                    AvailableMobTypes.RemoveAt(i);
                }
            }

            foreach (var item in grouped)
            {
                if (existingMap.TryGetValue(item.TemplateId, out var existing))
                {
                    existing.Count = item.Count;
                    existing.Name = item.Name;
                    existing.Hp = item.Hp;
                    existing.MaxHp = item.MaxHp;
                    existing.IsFlying = item.IsFlying;
                    existing.IsSuperMob = item.IsSuperMob;
                }
                else
                {
                    AvailableMobTypes.Add(new MobFilterItem(
                        item.TemplateId,
                        item.Name,
                        item.Hp,
                        item.MaxHp,
                        item.IsFlying,
                        item.IsSuperMob,
                        item.Count,
                        true
                    ));
                }
            }
        }
        else
        {
            var existingMap = AvailableMobTypes
                .Where(m => m.MobId.HasValue)
                .ToDictionary(m => m.MobId!.Value);
            var currentMobIds = mobs.Select(m => m.Id).ToHashSet();

            for (int i = AvailableMobTypes.Count - 1; i >= 0; i--)
            {
                var item = AvailableMobTypes[i];
                if (item.IsCustom) continue;
                if (!item.MobId.HasValue || !currentMobIds.Contains(item.MobId.Value))
                {
                    AvailableMobTypes.RemoveAt(i);
                }
            }

            foreach (var mob in mobs.OrderBy(m => m.Id))
            {
                if (existingMap.TryGetValue(mob.Id, out var existing))
                {
                    existing.Name = mob.Name;
                    existing.Hp = mob.Hp;
                    existing.MaxHp = mob.MaxHp;
                    existing.IsFlying = mob.IsFlying;
                    existing.IsSuperMob = mob.IsSuperMob;
                }
                else
                {
                    AvailableMobTypes.Add(new MobFilterItem(
                        mob.TemplateId,
                        mob.Name,
                        mob.Hp,
                        mob.MaxHp,
                        mob.IsFlying,
                        mob.IsSuperMob,
                        1,
                        true,
                        false,
                        mob.Id
                    ));
                }
            }
        }

        FilteredMobTypes.Refresh();
    }

    private void RefreshAutoTrainSkills()
    {
        if (ActiveAccount == null || !ActiveAccount.IsRunning) return;

        var skills = _gameEngine.GetSkills(ActiveAccount.Id);
        if (skills.Count == 0) return;

        foreach (var skill in skills)
        {
            if (skill.Slot >= 0 && skill.Slot < AutoTrainSkills.Count)
            {
                if (!string.IsNullOrWhiteSpace(skill.Name))
                {
                    AutoTrainSkills[skill.Slot].Name = skill.Name;
                }
            }
        }
    }

    public void UpdateSyncTargetDisplay()
    {
        var targets = GetTargetAccountIds();
        int count = targets.Count;
        SyncTargetDisplay = count > 1 ? $"Đồng bộ: {count} acc" : (count == 1 ? "1 tài khoản" : "Chưa chọn acc");
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

    #region Auto Train Logic

    [RelayCommand]
    public void ToggleAutoTrain()
    {
        if (IsAutoTrainRunning)
        {
            StopAutoTrain();
        }
        else
        {
            StartAutoTrain();
        }
    }

    [RelayCommand]
    public void ClearMobSearch()
    {
        MobSearchText = string.Empty;
    }

    [RelayCommand]
    public void SelectAllMobs()
    {
        if (!string.IsNullOrWhiteSpace(MobSearchText))
        {
            foreach (var obj in FilteredMobTypes)
            {
                if (obj is MobFilterItem item)
                {
                    item.IsSelected = true;
                }
            }
        }
        else
        {
            foreach (var item in AvailableMobTypes)
            {
                item.IsSelected = true;
            }
        }
    }

    [RelayCommand]
    public void DeselectAllMobs()
    {
        if (!string.IsNullOrWhiteSpace(MobSearchText))
        {
            foreach (var obj in FilteredMobTypes)
            {
                if (obj is MobFilterItem item)
                {
                    item.IsSelected = false;
                }
            }
        }
        else
        {
            foreach (var item in AvailableMobTypes)
            {
                item.IsSelected = false;
            }
        }
    }

    [RelayCommand]
    public void AddCustomMobId()
    {
        if (string.IsNullOrWhiteSpace(CustomMobIdInput)) return;

        var tokens = CustomMobIdInput.Split([',', ';', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        var snapshot = _gameEngine.GetWorldSnapshot(ActiveAccount?.Id ?? Guid.Empty);

        foreach (var token in tokens)
        {
            string clean = token.Trim().TrimStart('#');
            if (int.TryParse(clean, out int id))
            {
                if (MobTargetModeIndex == 0)
                {
                    var existing = AvailableMobTypes.FirstOrDefault(m => !m.MobId.HasValue && m.TemplateId == id);
                    if (existing != null)
                    {
                        existing.IsSelected = true;
                    }
                    else
                    {
                        var match = snapshot?.Mobs.FirstOrDefault(m => m.TemplateId == id);
                        string mobName = match != null ? match.Name : $"Mục tiêu #{id}";
                        long hp = match?.Hp ?? 0;
                        long maxHp = match?.MaxHp ?? 0;
                        bool isFlying = match?.IsFlying ?? false;
                        bool isSuperMob = match?.IsSuperMob ?? false;

                        AvailableMobTypes.Add(new MobFilterItem(
                            templateId: id,
                            name: mobName,
                            hp: hp,
                            maxHp: maxHp,
                            isFlying: isFlying,
                            isSuperMob: isSuperMob,
                            count: 1,
                            isSelected: true,
                            isCustom: true
                        ));
                    }
                }
                else
                {
                    var existing = AvailableMobTypes.FirstOrDefault(m => m.MobId == id);
                    if (existing != null)
                    {
                        existing.IsSelected = true;
                    }
                    else
                    {
                        var match = snapshot?.Mobs.FirstOrDefault(m => m.Id == id);
                        string mobName = match != null ? match.Name : $"Mục tiêu #{id}";
                        long hp = match?.Hp ?? 0;
                        long maxHp = match?.MaxHp ?? 0;
                        bool isFlying = match?.IsFlying ?? false;
                        bool isSuperMob = match?.IsSuperMob ?? false;
                        int templateId = match?.TemplateId ?? -1;

                        AvailableMobTypes.Add(new MobFilterItem(
                            templateId: templateId,
                            name: mobName,
                            hp: hp,
                            maxHp: maxHp,
                            isFlying: isFlying,
                            isSuperMob: isSuperMob,
                            count: 1,
                            isSelected: true,
                            isCustom: true,
                            mobId: id
                        ));
                    }
                }
            }
        }

        CustomMobIdInput = string.Empty;
        FilteredMobTypes.Refresh();
    }

    [RelayCommand]
    public void RemoveCustomMob(MobFilterItem? item)
    {
        if (item != null && item.IsCustom)
        {
            AvailableMobTypes.Remove(item);
            FilteredMobTypes.Refresh();
        }
    }

    [RelayCommand]
    public void SelectAllSkills()
    {
        foreach (var item in AutoTrainSkills)
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    public void DeselectAllSkills()
    {
        foreach (var item in AutoTrainSkills)
        {
            item.IsSelected = false;
        }
    }

    private void StartAutoTrain()
    {
        var targets = AutoTrainSyncAll ? GetTargetAccountIds() : (ActiveAccount != null && ActiveAccount.IsRunning ? [ActiveAccount.Id] : []);
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            AutoTrainStatusText = "Chưa chọn tài khoản!";
            return;
        }

        var selectedSlots = AutoTrainSkills.Where(s => s.IsSelected).Select(s => s.SlotIndex).ToList();
        if (selectedSlots.Count == 0)
        {
            selectedSlots = [0];
        }

        var selectedMobTemplates = new List<int>();
        var selectedMobIds = new List<int>();

        if (MobTargetModeIndex == 0)
        {
            selectedMobTemplates = AvailableMobTypes
                .Where(m => m.IsSelected && !m.MobId.HasValue && m.TemplateId >= 0)
                .Select(m => m.TemplateId)
                .Distinct()
                .ToList();
        }
        else
        {
            selectedMobIds = AvailableMobTypes
                .Where(m => m.IsSelected && m.MobId.HasValue)
                .Select(m => m.MobId!.Value)
                .Distinct()
                .ToList();
        }

        var options = new AutoTrainOptions
        {
            SelectedSkillSlots = selectedSlots,
            TargetMobTemplateIds = selectedMobTemplates,
            TargetMobIds = selectedMobIds,
            IgnoreBosses = IgnoreBosses,
            AvoidSuperMobs = AvoidSuperMobs,
            AutoPickItems = AutoPickItems,
            AutoUsePean = AutoUsePean,
            UseTdlt = UseTdlt
        };

        IsAutoTrainRunning = true;
        AutoTrainStatusText = $"Đang khởi động ({targets.Count} acc)...";

        foreach (var id in targets)
        {
            _gameEngine.StartAutoTrain(id, options);
        }
    }

    private void StopAutoTrain()
    {
        var targets = AutoTrainSyncAll ? GetTargetAccountIds() : (ActiveAccount != null && ActiveAccount.IsRunning ? [ActiveAccount.Id] : []);
        foreach (var id in targets)
        {
            _gameEngine.StopAutoTrain(id);
        }
        IsAutoTrainRunning = false;
        AutoTrainStatusText = "Đã dừng tự động luyện tập.";
    }

    private void HandleAutoTrainStatusChanged(Guid accountId, string message)
    {
        var targets = AutoTrainSyncAll ? GetTargetAccountIds() : (ActiveAccount != null ? [ActiveAccount.Id] : []);
        if (!targets.Contains(accountId)) return;

        var dispatcher = App.Current?.Dispatcher;
        if (dispatcher == null) return;

        _ = dispatcher.InvokeAsync(() =>
        {
            AutoTrainStatusText = message;
        });
    }

    private void HandleAutoTrainStateChanged(Guid accountId, bool isRunning)
    {
        var targets = AutoTrainSyncAll ? GetTargetAccountIds() : (ActiveAccount != null ? [ActiveAccount.Id] : []);
        if (!targets.Contains(accountId)) return;

        var dispatcher = App.Current?.Dispatcher;
        if (dispatcher == null) return;

        _ = dispatcher.InvokeAsync(() =>
        {
            if (AutoTrainSyncAll)
            {
                IsAutoTrainRunning = targets.Any(id => _gameEngine.IsAutoTrainRunning(id));
            }
            else
            {
                IsAutoTrainRunning = isRunning;
            }
        });
    }

    partial void OnAutoTrainSyncAllChanged(bool value)
    {
        if (value)
        {
            IsAutoTrainRunning = GetTargetAccountIds().Any(id => _gameEngine.IsAutoTrainRunning(id));
        }
        else
        {
            IsAutoTrainRunning = ActiveAccount != null && _gameEngine.IsAutoTrainRunning(ActiveAccount.Id);
        }
    }

    #endregion

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
