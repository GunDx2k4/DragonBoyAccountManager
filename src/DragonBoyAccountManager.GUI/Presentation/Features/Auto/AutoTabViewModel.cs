using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Engine.Events;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTabViewModel : AccountAwareTabViewModel
{
    private readonly IGameImageService _gameImageService;
    private readonly IAccountStorageService _storageService;
    private readonly DispatcherTimer _cooldownTimer;
    private bool _isSwitchingAccount;

    [ObservableProperty]
    private bool _isAutoTrainRunning;

    [ObservableProperty]
    private string _autoTrainStatusText = "Chưa kích hoạt";

    [ObservableProperty]
    private bool _autoTrainSyncAll = false;

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
    private bool _autoChatSyncAll = false;

    [ObservableProperty]
    private bool _isAutoChatRunning;

    [ObservableProperty]
    private int _chatSentCount;

    [ObservableProperty]
    private string _autoChatStatusText = "Chưa kích hoạt";

    [ObservableProperty]
    private string _lastChatSentTime = string.Empty;

    public AutoTabViewModel(
        IGameEngineService gameEngine,
        IGameImageService gameImageService,
        IAccountStorageService storageService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, selectionService, dispatcher)
    {
        _gameImageService = gameImageService;
        _storageService = storageService;

        FilteredMobTypes = CollectionViewSource.GetDefaultView(AvailableMobTypes);
        FilteredMobTypes.Filter = FilterMobItem;

        for (int i = 0; i < 10; i++)
        {
            var skill = new SkillButtonItem(i, i == 9 ? "Skill 0" : $"Skill {i + 1}", i == 0);
            skill.PropertyChanged += OnSkillItemPropertyChanged;
            AutoTrainSkills.Add(skill);
        }

        _cooldownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _cooldownTimer.Tick += (_, _) => UpdateCooldowns();

        _gameEngine.OnSessionEvent += HandleSessionEvent;
    }

    public override void OnTabActivated()
    {
        StartPolling();
        RefreshData();
        UpdateSyncTargetDisplay();
    }

    public override void OnTabDeactivated()
    {
        SaveCurrentAccountSettings();
        StopPolling();
    }

    protected override void OnActiveSnapshotUpdated(GameSessionSnapshot snapshot)
    {
        if (snapshot.World.Mobs.Count > 0)
        {
            UpdateAvailableMobTypes(snapshot.World.Mobs);
        }
    }

    protected override void OnActiveAccountOffline()
    {
        AvailableMobTypes.Clear();
        FilteredMobTypes.Refresh();
        IsAutoTrainRunning = false;
        IsAutoChatRunning = false;
        AutoTrainStatusText = "Chưa kích hoạt";
        AutoChatStatusText = "Chưa kích hoạt";
    }

    private void HandleSessionEvent(SessionEvent evt)
    {
        switch (evt)
        {
            case BehaviorProgressEvent progress when progress.BehaviorId == AutoTrainBehavior.BehaviorId:
                HandleAutoTrainStatusChanged(progress.AccountId, progress.Message);
                break;
            case BehaviorProgressEvent progress when progress.BehaviorId == AutoChatBehavior.BehaviorId:
                HandleChatSent(progress.AccountId, progress.Message);
                break;
            case BehaviorStateChangedEvent state when state.BehaviorId == AutoTrainBehavior.BehaviorId:
                HandleAutoTrainStateChanged(state.AccountId, state.IsRunning);
                break;
            case BehaviorStateChangedEvent state when state.BehaviorId == AutoChatBehavior.BehaviorId:
                HandleAutoChatStateChanged(state.AccountId, state.IsRunning);
                break;
        }
    }

    public void StartPolling()
    {
        if (!_cooldownTimer.IsEnabled)
        {
            _cooldownTimer.Start();
        }
    }

    public void StopPolling()
    {
        if (_cooldownTimer.IsEnabled)
        {
            _cooldownTimer.Stop();
        }
    }

    public void SaveCurrentAccountSettings()
    {
        if (_isSwitchingAccount || ActiveAccount == null) return;

        var s = ActiveAccount.AutoSettings;
        s.IgnoreBosses = IgnoreBosses;
        s.AvoidSuperMobs = AvoidSuperMobs;
        s.AutoPickItems = AutoPickItems;
        s.AutoUsePean = AutoUsePean;
        s.UseTdlt = UseTdlt;
        s.TargetMode = (MobTargetMode)MobTargetModeIndex;

        s.SelectedSkillSlots = AutoTrainSkills
            .Where(x => x.IsSelected)
            .Select(x => x.SlotIndex)
            .ToList();

        s.TargetMobTemplateIds = AvailableMobTypes
            .Where(m => m.IsSelected && !m.MobId.HasValue && m.TemplateId >= 0)
            .Select(m => m.TemplateId)
            .Distinct()
            .ToList();

        s.TargetMobIds = AvailableMobTypes
            .Where(m => m.IsSelected && m.MobId.HasValue)
            .Select(m => m.MobId!.Value)
            .Distinct()
            .ToList();

        s.ChatText = ChatText;
        s.ChatDelaySeconds = ChatDelaySeconds;
        s.ChatScope = (ChatType)SelectedChatTypeIndex;
        s.IsRandomChat = IsRandomChat;

        _ = _storageService.AddOrUpdateAsync(ActiveAccount.Model);
    }

    private void LoadAccountSettings(AccountItemViewModel account)
    {
        _isSwitchingAccount = true;
        try
        {
            var s = account.AutoSettings;
            IgnoreBosses = s.IgnoreBosses;
            AvoidSuperMobs = s.AvoidSuperMobs;
            AutoPickItems = s.AutoPickItems;
            AutoUsePean = s.AutoUsePean;
            UseTdlt = s.UseTdlt;
            MobTargetModeIndex = (int)s.TargetMode;

            ChatText = s.ChatText ?? "Chào mọi người!";
            ChatDelaySeconds = s.ChatDelaySeconds <= 0 ? 5 : s.ChatDelaySeconds;
            SelectedChatTypeIndex = (int)s.ChatScope;
            IsRandomChat = s.IsRandomChat;

            var selectedSlots = s.SelectedSkillSlots != null && s.SelectedSkillSlots.Count > 0
                ? s.SelectedSkillSlots.ToHashSet()
                : [0];

            foreach (var item in AutoTrainSkills)
            {
                item.IsSelected = selectedSlots.Contains(item.SlotIndex);
            }

            IsAutoTrainRunning = _gameEngine.IsAutoTrainRunning(account.Id);
            account.IsAutoTrainRunning = IsAutoTrainRunning;
            AutoTrainStatusText = !string.IsNullOrWhiteSpace(account.AutoTrainStatusText)
                ? account.AutoTrainStatusText
                : (IsAutoTrainRunning ? "Đang chạy..." : "Chưa kích hoạt");

            IsAutoChatRunning = _gameEngine.IsAutoChatRunning(account.Id);
            account.IsAutoChatRunning = IsAutoChatRunning;
            AutoChatStatusText = !string.IsNullOrWhiteSpace(account.AutoChatStatusText)
                ? account.AutoChatStatusText
                : (IsAutoChatRunning ? "Đang chạy auto chat..." : "Chưa kích hoạt");
            ChatSentCount = account.ChatSentCount;
        }
        finally
        {
            _isSwitchingAccount = false;
        }
    }

    public override void SetActiveAccount(AccountItemViewModel? account)
    {
        if (ActiveAccount == account) return;

        if (ActiveAccount != null)
        {
            SaveCurrentAccountSettings();
        }

        ActiveAccount = account;
        HasActiveAccount = account != null;
        IsAccountRunning = account?.IsRunning == true;

        if (account == null)
        {
            ActiveAccountInfo = "Chưa chọn tài khoản";
            AvailableMobTypes.Clear();
            FilteredMobTypes.Refresh();
            IsAutoChatRunning = false;
            IsAutoTrainRunning = false;
            AutoTrainStatusText = "Chưa kích hoạt";
            AutoChatStatusText = "Chưa kích hoạt";
        }
        else
        {
            ActiveAccountInfo = FormatAccountDisplayInfo(account);

            LoadAccountSettings(account);
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
        OnAutoTrainOptionChanged();
        RefreshMobList();
    }

    partial void OnIgnoreBossesChanged(bool value) => OnAutoTrainOptionChanged();
    partial void OnAvoidSuperMobsChanged(bool value) => OnAutoTrainOptionChanged();
    partial void OnAutoPickItemsChanged(bool value) => OnAutoTrainOptionChanged();
    partial void OnAutoUsePeanChanged(bool value) => OnAutoTrainOptionChanged();
    partial void OnUseTdltChanged(bool value) => OnAutoTrainOptionChanged();

    partial void OnChatTextChanged(string value) => OnAutoChatConfigChanged();
    partial void OnChatDelaySecondsChanged(int value) => OnAutoChatConfigChanged();
    partial void OnSelectedChatTypeIndexChanged(int value) => OnAutoChatConfigChanged();
    partial void OnIsRandomChatChanged(bool value) => OnAutoChatConfigChanged();

    private void OnSkillItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkillButtonItem.IsSelected))
        {
            OnAutoTrainOptionChanged();
        }
    }

    private void OnMobItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MobFilterItem.IsSelected))
        {
            OnAutoTrainOptionChanged();
        }
    }

    private void OnAutoTrainOptionChanged()
    {
        if (_isSwitchingAccount || ActiveAccount == null) return;

        SaveCurrentAccountSettings();

        if (IsAutoTrainRunning)
        {
            var options = BuildCurrentUIOptions();
            _gameEngine.UpdateAutoTrainOptions(ActiveAccount.Id, options);
        }
    }

    private void OnAutoChatConfigChanged()
    {
        if (_isSwitchingAccount || ActiveAccount == null) return;

        SaveCurrentAccountSettings();

        if (IsAutoChatRunning)
        {
            var lines = ChatText
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();
            var interval = TimeSpan.FromSeconds(Math.Max(1, ChatDelaySeconds));
            var chatType = (ChatType)SelectedChatTypeIndex;

            _gameEngine.UpdateAutoChatConfig(ActiveAccount.Id, lines, interval, chatType, IsRandomChat);
        }
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
        var savedTemplateIds = ActiveAccount?.AutoSettings.TargetMobTemplateIds ?? [];
        var savedMobIds = ActiveAccount?.AutoSettings.TargetMobIds ?? [];

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
                    item.PropertyChanged -= OnMobItemPropertyChanged;
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
                    bool isSelected = savedTemplateIds.Count == 0 || savedTemplateIds.Contains(item.TemplateId);
                    var mobItem = new MobFilterItem(
                        item.TemplateId,
                        item.Name,
                        item.Hp,
                        item.MaxHp,
                        item.IsFlying,
                        item.IsSuperMob,
                        item.Count,
                        isSelected
                    );
                    mobItem.PropertyChanged += OnMobItemPropertyChanged;
                    AvailableMobTypes.Add(mobItem);
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
                    item.PropertyChanged -= OnMobItemPropertyChanged;
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
                    bool isSelected = savedMobIds.Count == 0 || savedMobIds.Contains(mob.Id);
                    var mobItem = new MobFilterItem(
                        mob.TemplateId,
                        mob.Name,
                        mob.Hp,
                        mob.MaxHp,
                        mob.IsFlying,
                        mob.IsSuperMob,
                        1,
                        isSelected,
                        false,
                        mob.Id
                    );
                    mobItem.PropertyChanged += OnMobItemPropertyChanged;
                    AvailableMobTypes.Add(mobItem);
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
                AutoTrainSkills[skill.Slot].SyncFrom(skill, _gameImageService, syncSelection: false);
            }
        }
        UpdateCooldowns();
    }

    private void UpdateCooldowns()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var item in AutoTrainSkills)
        {
            item.UpdateCooldown(now);
        }
    }

    [RelayCommand]
    public async Task UseSkillSlotAsync(SkillButtonItem? item)
    {
        if (item == null || ActiveAccount == null || !ActiveAccount.IsRunning) return;
        await _gameEngine.SelectSkillAsync(ActiveAccount.Id, item.SlotIndex);
        item.LastTimeUse = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        item.UpdateCooldown(item.LastTimeUse);
    }
}
