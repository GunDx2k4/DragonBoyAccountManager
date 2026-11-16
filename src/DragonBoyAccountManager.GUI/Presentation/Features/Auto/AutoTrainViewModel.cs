using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTrainViewModel : AutoFeatureViewModelBase
{
    private readonly IGameImageService _gameImageService;
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

    public AutoTrainViewModel(
        IGameEngineService gameEngine,
        IGameImageService gameImageService,
        IAccountStorageService storageService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, storageService, selectionService, dispatcher)
    {
        _gameImageService = gameImageService;

        InitializeSkills();
        InitializeItems();

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
            for (int i = 0; i < AutoTrainSkills.Count; i++)
            {
                AutoTrainSkills[i].Reset(resetSelection: false);
            }
        });
        for (int i = 0; i < AutoTrainItems.Count; i++)
        {
            AutoTrainItems[i].Quantity = 0;
        }
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
            for (int i = 0; i < AutoTrainSkills.Count; i++)
            {
                var item = AutoTrainSkills[i];
                item.Reset(resetSelection: false);
                item.IsSelected = selectedSlots.Contains(item.SlotIndex);
            }

            for (int i = 0; i < AutoTrainItems.Count; i++)
            {
                var item = AutoTrainItems[i];
                item.IsSelected = selectedItemIds.Contains(item.TemplateId);
            }

            for (int i = 0; i < AutoTrainBuas.Count; i++)
            {
                var item = AutoTrainBuas[i];
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
        for (int i = 0; i < AutoTrainItems.Count; i++)
        {
            AutoTrainItems[i].EnsureIconLoaded(_gameImageService);
        }

        for (int i = 0; i < AutoTrainBuas.Count; i++)
        {
            AutoTrainBuas[i].EnsureIconLoaded(_gameImageService);
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
        else if (propertyName is nameof(CheckAllMobs))
        {
            RunBulkUpdate(() =>
            {
                for (int i = 0; i < AvailableMobTypes.Count; i++)
                {
                    AvailableMobTypes[i].IsSelected = true;
                }
            });
        }
    }

    protected override void OnOptionChanged()
    {
        if (IsSwitchingAccount || IsBulkUpdating || ActiveAccount == null)
        {
            return;
        }

        _optionDebounceTimer.Stop();
        _hasPendingDebouncedSync = false;

        PersistActiveAccountSettings();

        var options = BuildCurrentOptions();
        var allTargets = GetAllTargetAccounts();
        for (int i = 0; i < allTargets.Count; i++)
        {
            var targetAcc = allTargets[i];
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

    private AutoTrainOptions BuildCurrentOptions()
    {
        var selectedSlots = new List<int>();
        for (int i = 0; i < AutoTrainSkills.Count; i++)
        {
            if (AutoTrainSkills[i].IsSelected)
            {
                selectedSlots.Add(AutoTrainSkills[i].SlotIndex);
            }
        }
        if (selectedSlots.Count == 0)
        {
            selectedSlots = [0];
        }

        var existingSavedNames = ActiveAccount?.AutoSettings.GetBehaviorOptions<AutoTrainOptions>(BehaviorId)?.TargetMobNames ?? [];
        IReadOnlyList<string> selectedMobNames;
        if (AvailableMobTypes.Count > 0)
        {
            var currentMapMobNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < AvailableMobTypes.Count; i++)
            {
                currentMapMobNames.Add(AvailableMobTypes[i].Name);
            }

            var mergedNames = new List<string>();
            for (int i = 0; i < existingSavedNames.Count; i++)
            {
                string n = existingSavedNames[i];
                if (!currentMapMobNames.Contains(n) && !mergedNames.Contains(n, StringComparer.OrdinalIgnoreCase))
                {
                    mergedNames.Add(n);
                }
            }

            for (int i = 0; i < AvailableMobTypes.Count; i++)
            {
                var mob = AvailableMobTypes[i];
                if (mob.IsSelected && !mergedNames.Contains(mob.Name, StringComparer.OrdinalIgnoreCase))
                {
                    mergedNames.Add(mob.Name);
                }
            }

            selectedMobNames = mergedNames;
        }
        else
        {
            selectedMobNames = existingSavedNames.ToList();
        }

        var selectedItemIds = new List<short>();
        for (int i = 0; i < AutoTrainItems.Count; i++)
        {
            if (AutoTrainItems[i].IsSelected)
            {
                selectedItemIds.Add(AutoTrainItems[i].TemplateId);
            }
        }

        var selectedBuaIds = new List<short>();
        for (int i = 0; i < AutoTrainBuas.Count; i++)
        {
            if (AutoTrainBuas[i].IsSelected)
            {
                selectedBuaIds.Add(AutoTrainBuas[i].TemplateId);
            }
        }

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
        for (int i = 0; i < targets.Count; i++)
        {
            var id = targets[i];
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
        for (int i = 0; i < targets.Count; i++)
        {
            var id = targets[i];
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
}
