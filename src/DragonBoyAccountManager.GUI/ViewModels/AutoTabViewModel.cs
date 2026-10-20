using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.GUI.Messaging;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.GameEngine;
using DragonBoyAccountManager.GUI.Services.Images;
using DragonBoyAccountManager.GUI.Services.Storage;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AutoTabViewModel : ObservableObject
{
    private readonly IGameEngineService _gameEngine;
    private readonly IGameImageService _gameImageService;
    private readonly IAccountStorageService _storageService;
    private readonly DispatcherTimer _cooldownTimer;
    private Func<List<Guid>>? _targetResolver;
    private Func<Guid, AccountItemViewModel?>? _accountResolver;
    private bool _isSwitchingAccount;

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
        IAccountStorageService storageService)
    {
        _gameEngine = gameEngine;
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

        _gameEngine.OnChatSent += HandleChatSent;
        _gameEngine.OnAutoTrainStatusChanged += HandleAutoTrainStatusChanged;
        _gameEngine.OnAutoTrainStateChanged += HandleAutoTrainStateChanged;

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
                            if (m.Snapshot.World.Mobs.Count > 0)
                            {
                                UpdateAvailableMobTypes(m.Snapshot.World.Mobs);
                            }
                        }
                        else if (m.Status == GameSessionStatus.Offline)
                        {
                            IsAccountRunning = false;
                            AvailableMobTypes.Clear();
                            FilteredMobTypes.Refresh();
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

    public void SetTargetResolver(Func<List<Guid>> resolver)
    {
        _targetResolver = resolver;
    }

    public void SetAccountResolver(Func<Guid, AccountItemViewModel?> resolver)
    {
        _accountResolver = resolver;
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
        s.MobTargetModeIndex = MobTargetModeIndex;

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
        s.SelectedChatTypeIndex = SelectedChatTypeIndex;
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
            MobTargetModeIndex = s.MobTargetModeIndex;

            ChatText = s.ChatText ?? "Chào mọi người!";
            ChatDelaySeconds = s.ChatDelaySeconds <= 0 ? 5 : s.ChatDelaySeconds;
            SelectedChatTypeIndex = s.SelectedChatTypeIndex;
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

    public void SetActiveAccount(AccountItemViewModel? account)
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
            string server = !string.IsNullOrWhiteSpace(account.Server) ? $" ({account.Server})" : "";
            string charName = !string.IsNullOrWhiteSpace(account.CharacterName) ? $" [{account.CharacterName}]" : "";
            ActiveAccountInfo = $"{account.Username}{server}{charName}";

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
                var item = AutoTrainSkills[skill.Slot];
                if (!string.IsNullOrWhiteSpace(skill.Name))
                {
                    item.Name = skill.Name;
                }
                item.Point = skill.Point;
                item.CoolDown = skill.CoolDown;
                item.ManaUse = skill.ManaUse;
                item.LastTimeUse = skill.LastTimeUse;
                if (skill.IconId >= 0 && item.IconId != skill.IconId)
                {
                    item.IconId = skill.IconId;
                    item.Icon = _gameImageService.GetSmallImage(skill.IconId);
                }
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
    public void UseSkillSlot(SkillButtonItem? item)
    {
        if (item == null || ActiveAccount == null || !ActiveAccount.IsRunning) return;
        _gameEngine.UseSkill(ActiveAccount.Id, item.SlotIndex);
        item.LastTimeUse = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        item.UpdateCooldown(item.LastTimeUse);
    }

    public void UpdateSyncTargetDisplay()
    {
        var targets = GetTargetAccountIds();
        int count = targets.Count;
        SyncTargetDisplay = count > 1 ? $"Đồng bộ: {count} tài khoản" : (count == 1 ? "1 tài khoản" : "Chưa chọn tài khoản");
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
        OnAutoTrainOptionChanged();
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
        OnAutoTrainOptionChanged();
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

                        var customItem = new MobFilterItem(
                            templateId: id,
                            name: mobName,
                            hp: hp,
                            maxHp: maxHp,
                            isFlying: isFlying,
                            isSuperMob: isSuperMob,
                            count: 1,
                            isSelected: true,
                            isCustom: true
                        );
                        customItem.PropertyChanged += OnMobItemPropertyChanged;
                        AvailableMobTypes.Add(customItem);
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

                        var customItem = new MobFilterItem(
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
                        );
                        customItem.PropertyChanged += OnMobItemPropertyChanged;
                        AvailableMobTypes.Add(customItem);
                    }
                }
            }
        }

        CustomMobIdInput = string.Empty;
        FilteredMobTypes.Refresh();
        OnAutoTrainOptionChanged();
    }

    [RelayCommand]
    public void RemoveCustomMob(MobFilterItem? item)
    {
        if (item != null && item.IsCustom)
        {
            item.PropertyChanged -= OnMobItemPropertyChanged;
            AvailableMobTypes.Remove(item);
            FilteredMobTypes.Refresh();
            OnAutoTrainOptionChanged();
        }
    }

    [RelayCommand]
    public void SelectAllSkills()
    {
        foreach (var item in AutoTrainSkills)
        {
            item.IsSelected = true;
        }
        OnAutoTrainOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllSkills()
    {
        foreach (var item in AutoTrainSkills)
        {
            item.IsSelected = false;
        }
        OnAutoTrainOptionChanged();
    }

    private AutoTrainOptions BuildCurrentUIOptions()
    {
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

        int minHp = ActiveAccount?.AutoSettings.MinHpPercent ?? 20;
        int minMp = ActiveAccount?.AutoSettings.MinMpPercent ?? 20;

        return new AutoTrainOptions
        {
            SelectedSkillSlots = selectedSlots,
            TargetMobTemplateIds = selectedMobTemplates,
            TargetMobIds = selectedMobIds,
            IgnoreBosses = IgnoreBosses,
            AvoidSuperMobs = AvoidSuperMobs,
            AutoPickItems = AutoPickItems,
            AutoUsePean = AutoUsePean,
            UseTdlt = UseTdlt,
            MinHpPercent = minHp,
            MinMpPercent = minMp
        };
    }

    private static AutoTrainOptions BuildOptionsForAccount(AccountItemViewModel account)
    {
        var s = account.AutoSettings;
        var selectedSlots = s.SelectedSkillSlots != null && s.SelectedSkillSlots.Count > 0 ? s.SelectedSkillSlots : [0];
        return new AutoTrainOptions
        {
            SelectedSkillSlots = selectedSlots,
            TargetMobTemplateIds = s.TargetMobTemplateIds ?? [],
            TargetMobIds = s.TargetMobIds ?? [],
            IgnoreBosses = s.IgnoreBosses,
            AvoidSuperMobs = s.AvoidSuperMobs,
            AutoPickItems = s.AutoPickItems,
            AutoUsePean = s.AutoUsePean,
            UseTdlt = s.UseTdlt,
            MinHpPercent = s.MinHpPercent,
            MinMpPercent = s.MinMpPercent
        };
    }

    private void StartAutoTrain()
    {
        SaveCurrentAccountSettings();

        var targets = AutoTrainSyncAll ? GetTargetAccountIds() : (ActiveAccount != null && ActiveAccount.IsRunning ? [ActiveAccount.Id] : []);
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản đang chạy nào!";
            AutoTrainStatusText = "Chưa chọn tài khoản!";
            return;
        }

        IsAutoTrainRunning = true;
        AutoTrainStatusText = targets.Count > 1 ? $"Đang khởi động ({targets.Count} tài khoản)..." : "Đang khởi động...";

        foreach (var id in targets)
        {
            var targetAcc = _accountResolver?.Invoke(id) ?? (ActiveAccount?.Id == id ? ActiveAccount : null);
            var options = targetAcc != null && targetAcc.Id != ActiveAccount?.Id
                ? BuildOptionsForAccount(targetAcc)
                : BuildCurrentUIOptions();

            if (targetAcc != null)
            {
                targetAcc.IsAutoTrainRunning = true;
                targetAcc.AutoTrainStatusText = "Đang khởi động...";
            }

            _gameEngine.StartAutoTrain(id, options);
        }
    }

    private void StopAutoTrain()
    {
        var targets = AutoTrainSyncAll ? GetTargetAccountIds() : (ActiveAccount != null && ActiveAccount.IsRunning ? [ActiveAccount.Id] : []);
        foreach (var id in targets)
        {
            _gameEngine.StopAutoTrain(id);
            var targetAcc = _accountResolver?.Invoke(id) ?? (ActiveAccount?.Id == id ? ActiveAccount : null);
            if (targetAcc != null)
            {
                targetAcc.IsAutoTrainRunning = false;
                targetAcc.AutoTrainStatusText = "Đã dừng tự động luyện tập.";
            }
        }
        IsAutoTrainRunning = false;
        AutoTrainStatusText = "Đã dừng tự động luyện tập.";
    }

    private void HandleAutoTrainStatusChanged(Guid accountId, string message)
    {
        var dispatcher = App.Current?.Dispatcher;
        dispatcher?.InvokeAsync(() =>
        {
            var target = _accountResolver?.Invoke(accountId);
            if (target != null)
            {
                target.AutoTrainStatusText = message;
            }

            if (ActiveAccount?.Id == accountId)
            {
                AutoTrainStatusText = message;
            }
        });
    }

    private void HandleAutoTrainStateChanged(Guid accountId, bool isRunning)
    {
        var dispatcher = App.Current?.Dispatcher;
        dispatcher?.InvokeAsync(() =>
        {
            var target = _accountResolver?.Invoke(accountId);
            if (target != null)
            {
                target.IsAutoTrainRunning = isRunning;
            }

            if (ActiveAccount?.Id == accountId)
            {
                IsAutoTrainRunning = isRunning;
            }
        });
    }

    partial void OnAutoTrainSyncAllChanged(bool value)
    {
        if (value)
        {
            var targets = GetTargetAccountIds();
            IsAutoTrainRunning = targets.Any(id => _gameEngine.IsAutoTrainRunning(id));
        }
        else
        {
            IsAutoTrainRunning = ActiveAccount != null && _gameEngine.IsAutoTrainRunning(ActiveAccount.Id);
            AutoTrainStatusText = ActiveAccount?.AutoTrainStatusText ?? (IsAutoTrainRunning ? "Đang chạy..." : "Chưa kích hoạt");
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
            StatusText = "Không có tài khoản nào trong game để gửi!";
            return;
        }

        int count = 0;
        foreach (var id in targets)
        {
            if (await _gameEngine.SendChatAsync(id, msg, chatType)) count++;
        }

        StatusText = count > 0 ? $"Đã gửi 1 tin chat tới {count} tài khoản lúc {DateTime.Now:HH:mm:ss}" : "Không thể gửi tin nhắn chat!";
    }

    private void StartAutoChat()
    {
        SaveCurrentAccountSettings();

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
        AutoChatStatusText = targets.Count > 1 ? $"Đang chạy auto chat ({targets.Count} tài khoản)..." : "Đang chạy auto chat...";

        foreach (var id in targets)
        {
            var targetAcc = _accountResolver?.Invoke(id) ?? (ActiveAccount?.Id == id ? ActiveAccount : null);
            if (targetAcc != null && targetAcc.Id != ActiveAccount?.Id)
            {
                var s = targetAcc.AutoSettings;
                var accLines = (s.ChatText ?? ChatText)
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToArray();
                var accInterval = TimeSpan.FromSeconds(Math.Max(1, s.ChatDelaySeconds <= 0 ? 5 : s.ChatDelaySeconds));
                var accChatType = (ChatType)s.SelectedChatTypeIndex;
                targetAcc.IsAutoChatRunning = true;
                targetAcc.AutoChatStatusText = "Đang chạy auto chat...";
                _gameEngine.StartAutoChat(id, accLines, accInterval, accChatType, s.IsRandomChat);
            }
            else
            {
                if (targetAcc != null)
                {
                    targetAcc.IsAutoChatRunning = true;
                    targetAcc.AutoChatStatusText = "Đang chạy auto chat...";
                }
                _gameEngine.StartAutoChat(id, lines, interval, chatType, IsRandomChat);
            }
        }
    }

    private void StopAutoChat()
    {
        var targets = AutoChatSyncAll ? GetTargetAccountIds() : (ActiveAccount != null && ActiveAccount.IsRunning ? [ActiveAccount.Id] : []);
        foreach (var id in targets)
        {
            _gameEngine.StopAutoChat(id);
            var targetAcc = _accountResolver?.Invoke(id) ?? (ActiveAccount?.Id == id ? ActiveAccount : null);
            if (targetAcc != null)
            {
                targetAcc.IsAutoChatRunning = false;
                targetAcc.AutoChatStatusText = "Đã dừng auto chat.";
            }
        }
        IsAutoChatRunning = false;
        AutoChatStatusText = $"Đã dừng (Đã gửi tổng cộng: {ChatSentCount} tin)";
    }

    private void HandleChatSent(Guid accountId, string message, int count)
    {
        var dispatcher = App.Current?.Dispatcher;
        dispatcher?.InvokeAsync(() =>
        {
            var target = _accountResolver?.Invoke(accountId);
            if (target != null)
            {
                target.ChatSentCount = count;
                target.AutoChatStatusText = $"Đang chạy... Đã gửi: {count} tin ({DateTime.Now:HH:mm:ss})";
            }

            if (ActiveAccount?.Id == accountId)
            {
                ChatSentCount = count;
                LastChatSentTime = DateTime.Now.ToString("HH:mm:ss");
                AutoChatStatusText = $"Đang chạy... Đã gửi: {count} tin ({LastChatSentTime})";
            }
        });
    }

    partial void OnAutoChatSyncAllChanged(bool value)
    {
        if (value)
        {
            var targets = GetTargetAccountIds();
            IsAutoChatRunning = targets.Any(id => _gameEngine.IsAutoChatRunning(id));
        }
        else
        {
            IsAutoChatRunning = ActiveAccount != null && _gameEngine.IsAutoChatRunning(ActiveAccount.Id);
            AutoChatStatusText = ActiveAccount?.AutoChatStatusText ?? (IsAutoChatRunning ? "Đang chạy..." : "Chưa kích hoạt");
        }
    }

    #endregion
}
