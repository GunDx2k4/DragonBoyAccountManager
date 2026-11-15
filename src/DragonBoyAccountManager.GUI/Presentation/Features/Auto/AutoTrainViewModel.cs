using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTrainViewModel : AutoFeatureViewModelBase
{
    private readonly IGameImageService _gameImageService;
    private readonly DispatcherTimer _cooldownTimer;
    private readonly DispatcherTimer _optionDebounceTimer;
    private Guid _lastSyncedOnlineAccountId;
    private bool _hasPendingDebouncedSync;

    public override string BehaviorId => AutoTrainBehavior.BehaviorId;

    [ObservableProperty]
    private bool _avoidSuperMobs;

    [ObservableProperty]
    private bool _changeZoneWhenNoMobs;

    [ObservableProperty]
    private bool _checkAttackByHp;

    [ObservableProperty]
    private long _minMobHp;

    [ObservableProperty]
    private long _maxMobHp = 10000000001L;

    [ObservableProperty]
    private bool _autoUsePean;

    [ObservableProperty]
    private int _peanHpPercent = 50;

    [ObservableProperty]
    private int _peanMpPercent = 50;

    [ObservableProperty]
    private bool _checkAllMobs = true;

    [ObservableProperty]
    private bool _autoPick;

    [ObservableProperty]
    private bool _pickOthersItems;

    [ObservableProperty]
    private bool _pickAllMap;

    [ObservableProperty]
    private bool _autoDropItems;

    [ObservableProperty]
    private string _filterPickIds = string.Empty;

    [ObservableProperty]
    private string _filterExcludeIds = string.Empty;

    [ObservableProperty]
    private string _filterDropIds = string.Empty;

    [ObservableProperty]
    private bool _hasSelectedAutoItems;

    [ObservableProperty]
    private string _itemStatusText = "Chưa chọn vật phẩm tự dùng";

    [ObservableProperty]
    private string _pickStatusText = "Chưa bật tự động nhặt";

    public ObservableCollection<MobFilterItem> AvailableMobTypes { get; } = [];
    public ObservableCollection<SkillButtonItem> AutoTrainSkills { get; } = [];
    public ObservableCollection<AutoItemSlotItem> AutoTrainItems { get; } = [];
    public ObservableCollection<AutoItemSlotItem> AutoTrainBuas { get; } = [];

    public AutoTrainViewModel(
        IGameEngineService gameEngine,
        IGameImageService gameImageService,
        IAccountStorageService storageService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, storageService, selectionService, dispatcher)
    {
        _gameImageService = gameImageService;

        for (int i = 0; i < 10; i++)
        {
            var skill = new SkillButtonItem(i, i == 9 ? "Skill 0" : $"Skill {i + 1}", i == 0);
            skill.PropertyChanged += OnSkillItemPropertyChanged;
            AutoTrainSkills.Add(skill);
        }

        foreach (var def in GameDefinitions.AutoTrainItems)
        {
            var autoItem = new AutoItemSlotItem(def.Index, def.TemplateId, def.IconId, def.Name);
            autoItem.PropertyChanged += OnAutoItemPropertyChanged;
            AutoTrainItems.Add(autoItem);
        }

        foreach (var def in GameDefinitions.AutoTrainBuas)
        {
            var autoBua = new AutoItemSlotItem(def.Index, def.TemplateId, def.IconId, def.Name);
            autoBua.PropertyChanged += OnAutoBuaPropertyChanged;
            AutoTrainBuas.Add(autoBua);
        }

        _cooldownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _cooldownTimer.Tick += (_, _) => UpdateCooldowns();

        _optionDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _optionDebounceTimer.Tick += (_, _) => FlushDebouncedOptionChanged();

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SyncTargetDisplay))
            {
                UpdateItemStatusText();
                UpdatePickStatus();
            }
        };
    }

    public override void OnActivated()
    {
        if (!_cooldownTimer.IsEnabled)
        {
            _cooldownTimer.Start();
        }
        RefreshData();
    }

    public override void OnDeactivated()
    {
        if (_cooldownTimer.IsEnabled)
        {
            _cooldownTimer.Stop();
        }
        FlushDebouncedOptionChanged();
    }

    public override void OnSnapshotUpdated(GameSessionSnapshot snapshot)
    {
        UpdateAvailableMobTypes(snapshot.World.Mobs);
        RefreshSkills();
        RefreshAutoItems(snapshot.Player.BagItemList);

        if (ActiveAccount != null && snapshot.Player.IsInGame && _lastSyncedOnlineAccountId != ActiveAccount.Id)
        {
            _lastSyncedOnlineAccountId = ActiveAccount.Id;
            if (!GameEngine.IsAutoTrainRunning(ActiveAccount.Id))
            {
                _ = GameEngine.ExecuteCommandAsync(ActiveAccount.Id, BuildInGameSyncCommand(BuildCurrentOptions()));
            }
        }
    }

    public override void OnAccountOffline()
    {
        base.OnAccountOffline();
        _optionDebounceTimer.Stop();
        _hasPendingDebouncedSync = false;
        _lastSyncedOnlineAccountId = Guid.Empty;
        ClearAvailableMobTypes();
        RunBulkUpdate(() =>
        {
            foreach (var item in AutoTrainSkills)
            {
                item.Reset(resetSelection: false);
            }
        });
        foreach (var item in AutoTrainItems)
        {
            item.Quantity = 0;
        }
    }

    private void ClearAvailableMobTypes()
    {
        foreach (var item in AvailableMobTypes)
        {
            item.PropertyChanged -= OnMobItemPropertyChanged;
        }
        AvailableMobTypes.Clear();
    }

    protected override void LoadAccountCore(AccountItemViewModel account)
    {
        _optionDebounceTimer.Stop();
        _hasPendingDebouncedSync = false;
        _lastSyncedOnlineAccountId = Guid.Empty;
        ClearAvailableMobTypes();

        var train = account.AutoSettings.GetBehaviorOptionsOrDefault<AutoTrainOptions>(BehaviorId);
        AvoidSuperMobs = train.AvoidSuperMobs;
        ChangeZoneWhenNoMobs = train.ChangeZoneWhenNoMobs;
        CheckAttackByHp = train.CheckAttackByHp;
        MinMobHp = train.MinMobHp;
        MaxMobHp = train.MaxMobHp;
        AutoUsePean = train.AutoUsePean;
        PeanHpPercent = train.MinHpPercent;
        PeanMpPercent = train.MinMpPercent;
        CheckAllMobs = train.CheckAllMobs;
        AutoPick = train.AutoPick;
        PickOthersItems = train.PickOthersItems;
        PickAllMap = train.PickAllMap;
        AutoDropItems = train.AutoDropItems;
        FilterPickIds = train.FilterPickIds ?? string.Empty;
        FilterExcludeIds = train.FilterExcludeIds ?? string.Empty;
        FilterDropIds = train.FilterDropIds ?? string.Empty;

        var selectedSlots = train.SelectedSkillSlots is { Count: > 0 }
            ? train.SelectedSkillSlots.ToHashSet()
            : [0];
        var selectedItemIds = train.SelectedItemTemplateIds is { Count: > 0 }
            ? train.SelectedItemTemplateIds.ToHashSet()
            : [];
        var selectedBuaIds = train.SelectedBuaTemplateIds is { Count: > 0 }
            ? train.SelectedBuaTemplateIds.ToHashSet()
            : [];

        RunBulkUpdate(() =>
        {
            foreach (var item in AutoTrainSkills)
            {
                item.Reset(resetSelection: false);
                item.IsSelected = selectedSlots.Contains(item.SlotIndex);
            }

            foreach (var item in AutoTrainItems)
            {
                item.IsSelected = selectedItemIds.Contains(item.TemplateId);
            }

            foreach (var item in AutoTrainBuas)
            {
                item.IsSelected = selectedBuaIds.Contains(item.TemplateId);
            }
        });

        HasSelectedAutoItems = selectedItemIds.Count > 0;
        UpdateItemStatusText();
        UpdatePickStatus();

        var state = account.GetBehaviorState(BehaviorId);
        IsRunning = GameEngine.IsAutoTrainRunning(account.Id);
        state.IsRunning = IsRunning;
        StatusText = !string.IsNullOrWhiteSpace(state.StatusText)
            ? state.StatusText
            : (IsRunning ? "Đang chạy..." : "Chưa kích hoạt");

        RefreshData();
    }

    protected override void SaveAccountCore(AccountItemViewModel account)
    {
        var options = BuildCurrentOptions();
        account.AutoSettings.SetBehaviorOptions(BehaviorId, options);

        if (_hasPendingDebouncedSync && ActiveAccount != null && account.Id == ActiveAccount.Id)
        {
            _optionDebounceTimer.Stop();
            _hasPendingDebouncedSync = false;
            SyncOptionsToEngine(account, options);

            foreach (var targetAcc in GetAllTargetAccounts())
            {
                if (targetAcc.Id != account.Id)
                {
                    targetAcc.AutoSettings.SetBehaviorOptions(BehaviorId, options);
                    _ = StorageService.AddOrUpdateAsync(targetAcc.Model);
                    SyncOptionsToEngine(targetAcc, options);
                }
            }
        }
    }

    public void RefreshData()
    {
        foreach (var item in AutoTrainItems)
        {
            item.EnsureIconLoaded(_gameImageService);
        }

        foreach (var item in AutoTrainBuas)
        {
            item.EnsureIconLoaded(_gameImageService);
        }

        if (ActiveAccount == null || !ActiveAccount.IsRunning || ActiveAccount.Status != GameSessionStatus.Active)
        {
            ClearAvailableMobTypes();
            return;
        }

        var snapshot = GameEngine.GetWorldSnapshot(ActiveAccount.Id);
        if (snapshot != null)
        {
            UpdateAvailableMobTypes(snapshot.Mobs);
        }

        var playerSnapshot = GameEngine.GetPlayerSnapshot(ActiveAccount.Id);
        if (playerSnapshot != null)
        {
            RefreshAutoItems(playerSnapshot.BagItemList);
        }

        RefreshSkills();
    }

    private static bool IsDebouncedOptionProperty(string? propertyName) =>
        propertyName is nameof(MinMobHp)
            or nameof(MaxMobHp)
            or nameof(PeanHpPercent)
            or nameof(PeanMpPercent)
            or nameof(FilterPickIds)
            or nameof(FilterExcludeIds)
            or nameof(FilterDropIds);

    protected override bool IsOptionProperty(string propertyName) =>
        base.IsOptionProperty(propertyName) &&
        !IsDebouncedOptionProperty(propertyName) &&
        propertyName is not (nameof(PickStatusText) or nameof(ItemStatusText) or nameof(HasSelectedAutoItems));

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (!IsSwitchingAccount
            && !IsBulkUpdating
            && ActiveAccount != null
            && IsDebouncedOptionProperty(e.PropertyName))
        {
            _hasPendingDebouncedSync = true;
            _optionDebounceTimer.Stop();
            _optionDebounceTimer.Start();
        }
    }

    private void FlushDebouncedOptionChanged()
    {
        _optionDebounceTimer.Stop();
        if (_hasPendingDebouncedSync)
        {
            _hasPendingDebouncedSync = false;
            OnOptionChanged();
        }
    }

    protected override void OnOptionPropertyChanged(string propertyName)
    {
        if (propertyName is nameof(AutoPick) or nameof(PickOthersItems) or nameof(PickAllMap) or nameof(AutoDropItems))
        {
            UpdatePickStatus();
        }
        else if (propertyName == nameof(CheckAllMobs)
            && !CheckAllMobs
            && AvailableMobTypes.Count > 0
            && AvailableMobTypes.All(m => !m.IsSelected))
        {
            RunBulkUpdate(() =>
            {
                foreach (var item in AvailableMobTypes)
                {
                    item.IsSelected = true;
                }
            });
        }
    }

    private void OnSkillItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsBulkUpdating && e.PropertyName == nameof(SkillButtonItem.IsSelected))
        {
            OnOptionChanged();
        }
    }

    private void OnMobItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsBulkUpdating && e.PropertyName == nameof(MobFilterItem.IsSelected))
        {
            OnOptionChanged();
        }
    }

    private void OnAutoItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsBulkUpdating && e.PropertyName == nameof(AutoItemSlotItem.IsSelected))
        {
            UpdateItemSelectionStatusAndSave();
        }
    }

    private void OnAutoBuaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!IsBulkUpdating && e.PropertyName == nameof(AutoItemSlotItem.IsSelected))
        {
            OnOptionChanged();
        }
    }

    private void UpdateItemStatusText()
    {
        int selectedCount = AutoTrainItems.Count(i => i.IsSelected);
        HasSelectedAutoItems = selectedCount > 0;
        var targets = GetTargetAccountIds();
        ItemStatusText = selectedCount > 0
            ? (targets.Count > 1
                ? $"Đã cấu hình {selectedCount} vật phẩm ({targets.Count} tài khoản)"
                : $"Đã cấu hình {selectedCount} vật phẩm tự dùng")
            : "Chưa chọn vật phẩm tự dùng";
    }

    private void UpdatePickStatus()
    {
        var targets = GetTargetAccountIds();
        string targetSuffix = targets.Count > 1 ? $" ({targets.Count} tài khoản)" : string.Empty;
        if (AutoPick)
        {
            var modes = new List<string>();
            if (PickAllMap) modes.Add("toàn map");
            if (PickOthersItems) modes.Add("người khác");
            if (AutoDropItems) modes.Add("tự vứt");
            string modeDesc = modes.Count > 0 ? $" ({string.Join(", ", modes)})" : string.Empty;
            PickStatusText = $"Tự động nhặt: Đang bật{modeDesc}{targetSuffix}";
        }
        else
        {
            PickStatusText = $"Chưa bật tự động nhặt{targetSuffix}";
        }
    }

    private void UpdateItemSelectionStatusAndSave()
    {
        UpdateItemStatusText();
        OnOptionChanged();
    }

    protected override void OnOptionChanged()
    {
        if (IsSwitchingAccount || IsBulkUpdating || ActiveAccount == null) return;

        _optionDebounceTimer.Stop();
        _hasPendingDebouncedSync = false;

        PersistActiveAccountSettings();

        var options = BuildCurrentOptions();
        var allTargets = GetAllTargetAccounts();
        foreach (var targetAcc in allTargets)
        {
            if (targetAcc.Id != ActiveAccount.Id)
            {
                SaveSettings(targetAcc);
            }
            SyncOptionsToEngine(targetAcc, options);
        }
    }

    private void SyncOptionsToEngine(AccountItemViewModel account, AutoTrainOptions options)
    {
        if (GameEngine.IsAutoTrainRunning(account.Id))
        {
            GameEngine.UpdateAutoTrainOptions(account.Id, options);
        }
        else if (account.IsRunning)
        {
            var cmd = BuildInGameSyncCommand(options);
            _ = GameEngine.ExecuteCommandAsync(account.Id, cmd);
        }
    }

    private static SetInGameAutoTrainCommand BuildInGameSyncCommand(AutoTrainOptions options) =>
        new(
            Enable: false,
            Reason: "DBAM_SaveConfig",
            AvoidSuperMobs: options.AvoidSuperMobs,
            ChangeZoneWhenNoMobs: options.ChangeZoneWhenNoMobs,
            CheckAttackByHp: options.CheckAttackByHp,
            MinMobHp: options.MinMobHp,
            MaxMobHp: options.MaxMobHp,
            AutoUsePean: false,
            PeanHpPercent: options.MinHpPercent,
            PeanMpPercent: options.MinMpPercent,
            CheckAllMobs: options.CheckAllMobs,
            TargetMobNames: options.TargetMobNames,
            SelectedSkillSlots: options.SelectedSkillSlots,
            SelectedItemTemplateIds: options.SelectedItemTemplateIds,
            SelectedBuaTemplateIds: options.SelectedBuaTemplateIds,
            AutoPick: options.AutoPick,
            PickOthersItems: options.PickOthersItems,
            PickAllMap: options.PickAllMap,
            AutoDropItems: options.AutoDropItems,
            FilterPickIds: options.FilterPickIds,
            FilterExcludeIds: options.FilterExcludeIds,
            FilterDropIds: options.FilterDropIds
        );

    [RelayCommand]
    public void ToggleAutoTrain()
    {
        if (IsRunning)
        {
            StopAutoTrain();
        }
        else
        {
            StartAutoTrain();
        }
    }

    [RelayCommand]
    public void SelectAllMobs()
    {
        RunBulkUpdate(() =>
        {
            foreach (var item in AvailableMobTypes)
            {
                item.IsSelected = true;
            }
        });
        OnOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllMobs()
    {
        RunBulkUpdate(() =>
        {
            foreach (var item in AvailableMobTypes)
            {
                item.IsSelected = false;
            }
        });
        OnOptionChanged();
    }

    [RelayCommand]
    public void SelectAllSkills()
    {
        RunBulkUpdate(() =>
        {
            foreach (var item in AutoTrainSkills)
            {
                item.IsSelected = item.TemplateId != 14;
            }
        });
        OnOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllSkills()
    {
        RunBulkUpdate(() =>
        {
            foreach (var item in AutoTrainSkills)
            {
                item.IsSelected = false;
            }
        });
        OnOptionChanged();
    }

    [RelayCommand]
    public void SelectAllItems()
    {
        RunBulkUpdate(() =>
        {
            foreach (var item in AutoTrainItems)
            {
                item.IsSelected = true;
            }
        });
        UpdateItemSelectionStatusAndSave();
    }

    [RelayCommand]
    public void DeselectAllItems()
    {
        RunBulkUpdate(() =>
        {
            foreach (var item in AutoTrainItems)
            {
                item.IsSelected = false;
            }
        });
        UpdateItemSelectionStatusAndSave();
    }

    [RelayCommand]
    public async Task UseSkillSlotAsync(SkillButtonItem? item)
    {
        if (item == null || ActiveAccount == null || !ActiveAccount.IsRunning) return;
        await GameEngine.SelectSkillAsync(ActiveAccount.Id, item.SlotIndex);
        item.LastTimeUse = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        item.UpdateCooldown(item.LastTimeUse);
    }

    private AutoTrainOptions BuildCurrentOptions()
    {
        var selectedSlots = AutoTrainSkills.Where(s => s.IsSelected).Select(s => s.SlotIndex).ToList();
        if (selectedSlots.Count == 0)
        {
            selectedSlots = [0];
        }

        var existingSavedNames = ActiveAccount?.AutoSettings.GetBehaviorOptions<AutoTrainOptions>(BehaviorId)?.TargetMobNames ?? [];
        IReadOnlyList<string> selectedMobNames;
        if (AvailableMobTypes.Count > 0)
        {
            var currentMapMobNames = AvailableMobTypes.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            selectedMobNames = existingSavedNames
                .Where(n => !currentMapMobNames.Contains(n))
                .Concat(AvailableMobTypes.Where(m => m.IsSelected).Select(m => m.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            selectedMobNames = existingSavedNames.ToList();
        }

        var selectedItemIds = AutoTrainItems.Where(i => i.IsSelected).Select(i => i.TemplateId).ToList();
        var selectedBuaIds = AutoTrainBuas.Where(i => i.IsSelected).Select(i => i.TemplateId).ToList();

        return new AutoTrainOptions
        {
            SelectedSkillSlots = selectedSlots,
            SelectedItemTemplateIds = selectedItemIds,
            SelectedBuaTemplateIds = selectedBuaIds,
            TargetMobNames = selectedMobNames,
            CheckAllMobs = CheckAllMobs,
            AvoidSuperMobs = AvoidSuperMobs,
            ChangeZoneWhenNoMobs = ChangeZoneWhenNoMobs,
            CheckAttackByHp = CheckAttackByHp,
            MinMobHp = MinMobHp,
            MaxMobHp = MaxMobHp,
            AutoUsePean = AutoUsePean,
            MinHpPercent = PeanHpPercent,
            MinMpPercent = PeanMpPercent,
            AutoPick = AutoPick,
            PickOthersItems = PickOthersItems,
            PickAllMap = PickAllMap,
            AutoDropItems = AutoDropItems,
            FilterPickIds = FilterPickIds,
            FilterExcludeIds = FilterExcludeIds,
            FilterDropIds = FilterDropIds
        };
    }

    private void StartAutoTrain()
    {
        FlushDebouncedOptionChanged();
        PersistActiveAccountSettings();

        var targets = GetTargetAccountIds();
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản!";
            return;
        }

        IsRunning = true;
        StatusText = targets.Count > 1 ? $"Đang khởi động ({targets.Count} tài khoản)..." : "Đang khởi động...";

        var currentOptions = BuildCurrentOptions();
        foreach (var id in targets)
        {
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null)
            {
                SaveSettings(targetAcc);
                var state = targetAcc.GetBehaviorState(BehaviorId);
                state.IsRunning = true;
                state.StatusText = "Đang khởi động...";
            }

            GameEngine.StartAutoTrain(id, currentOptions);
        }
    }

    private void StopAutoTrain()
    {
        var targets = GetTargetAccountIds();
        foreach (var id in targets)
        {
            GameEngine.StopAutoTrain(id);
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null)
            {
                var state = targetAcc.GetBehaviorState(BehaviorId);
                state.IsRunning = false;
                state.StatusText = "Đã dừng tự động luyện tập.";
            }
        }
        IsRunning = false;
        StatusText = "Đã dừng tự động luyện tập.";
    }

    private void UpdateAvailableMobTypes(IReadOnlyList<WorldMobInfo> mobs)
    {
        var savedMobNames = ActiveAccount?.AutoSettings.GetBehaviorOptionsOrDefault<AutoTrainOptions>(BehaviorId).TargetMobNames ?? [];
        var savedSet = savedMobNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var distinctNames = mobs.Select(m => m.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var existingMap = AvailableMobTypes.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var currentNames = distinctNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        RunBulkUpdate(() =>
        {
            for (int i = AvailableMobTypes.Count - 1; i >= 0; i--)
            {
                var item = AvailableMobTypes[i];
                if (!currentNames.Contains(item.Name))
                {
                    item.PropertyChanged -= OnMobItemPropertyChanged;
                    AvailableMobTypes.RemoveAt(i);
                }
            }

            foreach (var name in distinctNames)
            {
                if (!existingMap.ContainsKey(name))
                {
                    bool isSelected = savedSet.Contains(name);
                    var mobItem = new MobFilterItem(
                        templateId: 0,
                        name: name,
                        isSelected: isSelected);
                    mobItem.PropertyChanged += OnMobItemPropertyChanged;
                    AvailableMobTypes.Add(mobItem);
                }
            }
        });
    }

    private void RefreshAutoItems(IReadOnlyList<InventoryItem> bagItems)
    {
        var countByTemplate = bagItems
            .GroupBy(b => b.TemplateId)
            .ToDictionary(g => g.Key, g => g.Sum(x => Math.Max(1, x.Quantity)));

        foreach (var item in AutoTrainItems)
        {
            item.EnsureIconLoaded(_gameImageService);
            item.Quantity = countByTemplate.GetValueOrDefault(item.TemplateId, 0);
        }
    }

    private void RefreshSkills()
    {
        if (ActiveAccount == null || !ActiveAccount.IsRunning) return;

        var skills = GameEngine.GetSkills(ActiveAccount.Id);
        if (skills.Count == 0)
        {
            foreach (var item in AutoTrainSkills)
            {
                item.Reset(resetSelection: false);
            }
            UpdateCooldowns();
            return;
        }

        Span<bool> populatedSlots = stackalloc bool[AutoTrainSkills.Count];
        foreach (var skill in skills)
        {
            if (skill.Slot >= 0 && skill.Slot < AutoTrainSkills.Count)
            {
                AutoTrainSkills[skill.Slot].SyncFrom(skill, _gameImageService, syncSelection: false);
                populatedSlots[skill.Slot] = true;
            }
        }

        for (int i = 0; i < AutoTrainSkills.Count; i++)
        {
            if (!populatedSlots[i])
            {
                AutoTrainSkills[i].Reset(resetSelection: false);
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
}
