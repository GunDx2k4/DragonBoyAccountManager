using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
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
    private bool _isBulkUpdating;

    public override string BehaviorId => AutoTrainBehavior.BehaviorId;

    [ObservableProperty]
    private bool _avoidSuperMobs = true;

    [ObservableProperty]
    private bool _changeZoneWhenNoMobs;

    [ObservableProperty]
    private bool _checkAttackByHp;

    [ObservableProperty]
    private long _minMobHp;

    [ObservableProperty]
    private long _maxMobHp = 10000000001L;

    [ObservableProperty]
    private bool _autoUsePean = true;

    [ObservableProperty]
    private int _peanHpPercent = 50;

    [ObservableProperty]
    private int _peanMpPercent = 50;

    [ObservableProperty]
    private bool _checkAllMobs = true;

    [ObservableProperty]
    private bool _hasSelectedAutoItems;

    [ObservableProperty]
    private string _itemStatusText = "Chưa chọn vật phẩm tự dùng";

    public ObservableCollection<MobFilterItem> AvailableMobTypes { get; } = [];
    public ObservableCollection<SkillButtonItem> AutoTrainSkills { get; } = [];
    public ObservableCollection<AutoItemSlotItem> AutoTrainItems { get; } = [];

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

        _cooldownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _cooldownTimer.Tick += (_, _) => UpdateCooldowns();
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
    }

    public override void OnSnapshotUpdated(GameSessionSnapshot snapshot)
    {
        if (snapshot.World.Mobs.Count > 0)
        {
            UpdateAvailableMobTypes(snapshot.World.Mobs);
        }
        RefreshSkills();
        RefreshAutoItems(snapshot.Player.BagItemList);
    }

    public override void OnAccountOffline()
    {
        base.OnAccountOffline();
        ClearAvailableMobTypes();
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

        var selectedSlots = train.SelectedSkillSlots is { Count: > 0 }
            ? train.SelectedSkillSlots.ToHashSet()
            : [0];

        foreach (var item in AutoTrainSkills)
        {
            item.IsSelected = selectedSlots.Contains(item.SlotIndex);
        }

        var selectedItemIds = train.SelectedItemTemplateIds is { Count: > 0 }
            ? train.SelectedItemTemplateIds.ToHashSet()
            : [];

        foreach (var item in AutoTrainItems)
        {
            item.IsSelected = selectedItemIds.Contains(item.TemplateId);
        }

        HasSelectedAutoItems = selectedItemIds.Count > 0;
        ItemStatusText = selectedItemIds.Count > 0
            ? $"Đã cấu hình {selectedItemIds.Count} vật phẩm tự dùng"
            : "Chưa chọn vật phẩm tự dùng";

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
        account.AutoSettings.SetBehaviorOptions(BehaviorId, BuildCurrentOptions());
    }

    public void RefreshData()
    {
        foreach (var item in AutoTrainItems)
        {
            item.EnsureIconLoaded(_gameImageService);
        }

        if (ActiveAccount == null || !ActiveAccount.IsRunning || ActiveAccount.Status != GameSessionStatus.Active)
        {
            ClearAvailableMobTypes();
            return;
        }

        var snapshot = GameEngine.GetWorldSnapshot(ActiveAccount.Id);
        if (snapshot != null && snapshot.Mobs.Count > 0)
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

    partial void OnAvoidSuperMobsChanged(bool value) => OnOptionChanged();
    partial void OnChangeZoneWhenNoMobsChanged(bool value) => OnOptionChanged();
    partial void OnCheckAttackByHpChanged(bool value) => OnOptionChanged();
    partial void OnMinMobHpChanged(long value) => OnOptionChanged();
    partial void OnMaxMobHpChanged(long value) => OnOptionChanged();
    partial void OnAutoUsePeanChanged(bool value) => OnOptionChanged();
    partial void OnPeanHpPercentChanged(int value) => OnOptionChanged();
    partial void OnPeanMpPercentChanged(int value) => OnOptionChanged();
    partial void OnCheckAllMobsChanged(bool value) => OnOptionChanged();

    private void OnSkillItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isBulkUpdating && e.PropertyName == nameof(SkillButtonItem.IsSelected))
        {
            OnOptionChanged();
        }
    }

    private void OnMobItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isBulkUpdating && e.PropertyName == nameof(MobFilterItem.IsSelected))
        {
            OnOptionChanged();
        }
    }

    private void OnAutoItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isBulkUpdating && e.PropertyName == nameof(AutoItemSlotItem.IsSelected))
        {
            UpdateItemSelectionStatusAndSave();
        }
    }

    private void UpdateItemSelectionStatusAndSave()
    {
        int selectedCount = AutoTrainItems.Count(i => i.IsSelected);
        HasSelectedAutoItems = selectedCount > 0;
        OnOptionChanged();
        var targets = GetTargetAccountIds();
        ItemStatusText = selectedCount > 0
            ? (targets.Count > 1
                ? $"Đã cấu hình {selectedCount} vật phẩm ({targets.Count} tài khoản)"
                : $"Đã cấu hình {selectedCount} vật phẩm tự dùng")
            : "Chưa chọn vật phẩm tự dùng";
    }

    private void OnOptionChanged()
    {
        if (IsSwitchingAccount || _isBulkUpdating || ActiveAccount == null) return;

        PersistActiveAccountSettings();

        var options = BuildCurrentOptions();
        foreach (var id in GetTargetAccountIds())
        {
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null && targetAcc.Id != ActiveAccount.Id)
            {
                SaveSettings(targetAcc);
            }
            if (targetAcc?.IsRunning == true)
            {
                GameEngine.UpdateAutoTrainOptions(id, options);
            }
        }
    }

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
        _isBulkUpdating = true;
        try
        {
            foreach (var item in AvailableMobTypes)
            {
                item.IsSelected = true;
            }
        }
        finally
        {
            _isBulkUpdating = false;
        }
        OnOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllMobs()
    {
        _isBulkUpdating = true;
        try
        {
            foreach (var item in AvailableMobTypes)
            {
                item.IsSelected = false;
            }
        }
        finally
        {
            _isBulkUpdating = false;
        }
        OnOptionChanged();
    }

    [RelayCommand]
    public void SelectAllSkills()
    {
        _isBulkUpdating = true;
        try
        {
            foreach (var item in AutoTrainSkills)
            {
                item.IsSelected = true;
            }
        }
        finally
        {
            _isBulkUpdating = false;
        }
        OnOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllSkills()
    {
        _isBulkUpdating = true;
        try
        {
            foreach (var item in AutoTrainSkills)
            {
                item.IsSelected = false;
            }
        }
        finally
        {
            _isBulkUpdating = false;
        }
        OnOptionChanged();
    }

    [RelayCommand]
    public void SelectAllItems()
    {
        _isBulkUpdating = true;
        try
        {
            foreach (var item in AutoTrainItems)
            {
                item.IsSelected = true;
            }
        }
        finally
        {
            _isBulkUpdating = false;
        }
        UpdateItemSelectionStatusAndSave();
    }

    [RelayCommand]
    public void DeselectAllItems()
    {
        _isBulkUpdating = true;
        try
        {
            foreach (var item in AutoTrainItems)
            {
                item.IsSelected = false;
            }
        }
        finally
        {
            _isBulkUpdating = false;
        }
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

        IReadOnlyList<string> selectedMobNames = AvailableMobTypes.Count > 0
            ? AvailableMobTypes.Where(m => m.IsSelected).Select(m => m.Name).ToList()
            : (ActiveAccount?.AutoSettings.GetBehaviorOptions<AutoTrainOptions>(BehaviorId)?.TargetMobNames ?? []);

        var selectedItemIds = AutoTrainItems.Where(i => i.IsSelected).Select(i => i.TemplateId).ToList();

        return new AutoTrainOptions
        {
            SelectedSkillSlots = selectedSlots,
            SelectedItemTemplateIds = selectedItemIds,
            TargetMobNames = selectedMobNames,
            CheckAllMobs = CheckAllMobs,
            AvoidSuperMobs = AvoidSuperMobs,
            ChangeZoneWhenNoMobs = ChangeZoneWhenNoMobs,
            CheckAttackByHp = CheckAttackByHp,
            MinMobHp = MinMobHp,
            MaxMobHp = MaxMobHp,
            AutoUsePean = AutoUsePean,
            MinHpPercent = PeanHpPercent,
            MinMpPercent = PeanMpPercent
        };
    }

    private void StartAutoTrain()
    {
        PersistActiveAccountSettings();

        var targets = GetTargetAccountIds();
        if (targets.Count == 0)
        {
            StatusText = "Chưa chọn tài khoản!";
            return;
        }

        IsRunning = true;
        StatusText = targets.Count > 1 ? $"Đang khởi động ({targets.Count} tài khoản)..." : "Đang khởi động...";

        foreach (var id in targets)
        {
            var targetAcc = ResolveAccount(id);
            var options = targetAcc != null && targetAcc.Id != ActiveAccount?.Id
                ? targetAcc.AutoSettings.GetBehaviorOptionsOrDefault<AutoTrainOptions>(BehaviorId)
                : BuildCurrentOptions();

            if (targetAcc != null)
            {
                var state = targetAcc.GetBehaviorState(BehaviorId);
                state.IsRunning = true;
                state.StatusText = "Đang khởi động...";
            }

            GameEngine.StartAutoTrain(id, options);
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
        var distinctNames = mobs.Select(m => m.Name).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToList();

        var existingMap = AvailableMobTypes.ToDictionary(m => m.Name);
        var currentNames = distinctNames.ToHashSet();

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
            if (!existingMap.TryGetValue(name, out _))
            {
                bool isSelected = savedMobNames.Count == 0 || savedMobNames.Contains(name);
                var mobItem = new MobFilterItem(
                    templateId: 0,
                    name: name,
                    isSelected: isSelected);
                mobItem.PropertyChanged += OnMobItemPropertyChanged;
                AvailableMobTypes.Add(mobItem);
            }
        }
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
}
