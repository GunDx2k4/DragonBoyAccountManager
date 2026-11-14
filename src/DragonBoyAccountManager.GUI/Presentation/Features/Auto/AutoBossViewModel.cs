using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Behaviors.Boss;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoBossViewModel : AutoFeatureViewModelBase
{
    private readonly IGameImageService _gameImageService;
    private readonly List<BossMapItem> _allMaps = [];
    private bool _isBulkUpdating;

    public override string BehaviorId => AutoBossBehavior.BehaviorId;

    public ObservableCollection<SkillButtonItem> BossSkills { get; } = [];
    public ObservableCollection<BossMapItem> FilteredMaps { get; } = [];
    public ObservableCollection<string> PlanetFilters { get; } = ["Tất cả", .. PlanetNames.All];

    [ObservableProperty]
    private string _targetBossName = "Zombie";

    [ObservableProperty]
    private int _minZone = 2;

    [ObservableProperty]
    private int _zoneChangeDelaySeconds = 11;

    [ObservableProperty]
    private bool _isFastTeleport = true;

    [ObservableProperty]
    private bool _pickOthersItems;

    [ObservableProperty]
    private string _filterPickNames = string.Empty;

    [ObservableProperty]
    private string _mapSearchText = string.Empty;

    [ObservableProperty]
    private string _selectedPlanetFilter = "Tất cả";

    [ObservableProperty]
    private int _selectedMapCount;

    public AutoBossViewModel(
        IGameEngineService gameEngine,
        IAccountStorageService storageService,
        IGameImageService gameImageService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, storageService, selectionService, dispatcher)
    {
        _gameImageService = gameImageService;

        for (int i = 0; i < 10; i++)
        {
            var skill = new SkillButtonItem(i, i == 9 ? "Skill 0" : $"Skill {i + 1}", i == 0);
            skill.PropertyChanged += OnSkillItemPropertyChanged;
            BossSkills.Add(skill);
        }

        InitializeMaps();
    }

    private void InitializeMaps()
    {
        _allMaps.Clear();
        foreach (var map in NextMapCatalog.Maps.Values.OrderBy(m => m.Id))
        {
            var item = new BossMapItem(map.Id, map.Name, map.PlanetId, OnMapSelectionChanged);
            _allMaps.Add(item);
        }
        ApplyMapFilter();
    }

    private void OnMapSelectionChanged()
    {
        if (_isBulkUpdating) return;
        SelectedMapCount = _allMaps.Count(m => m.IsSelected);
        OnOptionChanged();
    }

    private void OnSkillItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SkillButtonItem.IsSelected))
        {
            OnOptionChanged();
        }
    }

    partial void OnTargetBossNameChanged(string value) => OnOptionChanged();
    partial void OnMinZoneChanged(int value) => OnOptionChanged();
    partial void OnZoneChangeDelaySecondsChanged(int value) => OnOptionChanged();
    partial void OnIsFastTeleportChanged(bool value) => OnOptionChanged();
    partial void OnPickOthersItemsChanged(bool value) => OnOptionChanged();
    partial void OnFilterPickNamesChanged(string value) => OnOptionChanged();

    partial void OnMapSearchTextChanged(string value) => ApplyMapFilter();
    partial void OnSelectedPlanetFilterChanged(string value) => ApplyMapFilter();

    private void ApplyMapFilter()
    {
        FilteredMaps.Clear();
        string search = MapSearchText?.Trim() ?? string.Empty;
        bool filterPlanet = !string.Equals(SelectedPlanetFilter, "Tất cả", StringComparison.OrdinalIgnoreCase);

        foreach (var item in _allMaps)
        {
            if (filterPlanet && !string.Equals(item.PlanetName, SelectedPlanetFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(search))
            {
                bool matchesName = item.MapName.Contains(search, StringComparison.OrdinalIgnoreCase);
                bool matchesId = item.MapId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
                if (!matchesName && !matchesId)
                {
                    continue;
                }
            }

            FilteredMaps.Add(item);
        }
    }

    private void OnOptionChanged()
    {
        if (IsSwitchingAccount || _isBulkUpdating) return;
        PersistActiveAccountSettings();

        if (IsRunning)
        {
            var currentOptions = BuildCurrentOptions();
            foreach (var id in GetTargetAccountIds())
            {
                GameEngine.UpdateBehaviorOptions<AutoBossBehavior, AutoBossOptions>(id, currentOptions);
            }
        }
    }

    protected override void LoadAccountCore(AccountItemViewModel account)
    {
        _isBulkUpdating = true;
        try
        {
            var options = account.AutoSettings.GetBehaviorOptionsOrDefault<AutoBossOptions>(BehaviorId);
            TargetBossName = options.TargetBossName;
            MinZone = options.MinZone;
            ZoneChangeDelaySeconds = options.ZoneChangeDelaySeconds;
            IsFastTeleport = options.IsFastTeleport;
            PickOthersItems = options.PickOthersItems;
            FilterPickNames = options.FilterPickNames;

            var selectedMapIds = new HashSet<int>(options.SelectedMapIds);
            foreach (var map in _allMaps)
            {
                map.IsSelected = selectedMapIds.Contains(map.MapId);
            }
            SelectedMapCount = selectedMapIds.Count;

            var selectedSkills = new HashSet<int>(options.SelectedSkillSlots.Count > 0 ? options.SelectedSkillSlots : [0]);
            foreach (var skill in BossSkills)
            {
                skill.IsSelected = selectedSkills.Contains(skill.SlotIndex);
            }

            var state = account.GetBehaviorState(BehaviorId);
            IsRunning = GameEngine.IsBehaviorRunning<AutoBossBehavior>(account.Id);
            state.IsRunning = IsRunning;
            StatusText = !string.IsNullOrWhiteSpace(state.StatusText)
                ? state.StatusText
                : (IsRunning ? "Đang chạy săn Boss..." : "Chưa kích hoạt");
        }
        finally
        {
            _isBulkUpdating = false;
        }

        RefreshSkills();
    }

    protected override void SaveAccountCore(AccountItemViewModel account)
    {
        account.AutoSettings.SetBehaviorOptions(BehaviorId, BuildCurrentOptions());
    }

    private AutoBossOptions BuildCurrentOptions()
    {
        var selectedSkills = BossSkills.Where(s => s.IsSelected).Select(s => s.SlotIndex).ToList();
        if (selectedSkills.Count == 0)
        {
            selectedSkills = [0];
        }

        return new AutoBossOptions
        {
            SelectedMapIds = _allMaps.Where(m => m.IsSelected).Select(m => m.MapId).ToList(),
            TargetBossName = TargetBossName,
            MinZone = MinZone,
            ZoneChangeDelaySeconds = ZoneChangeDelaySeconds,
            IsFastTeleport = IsFastTeleport,
            PickOthersItems = PickOthersItems,
            FilterPickNames = FilterPickNames,
            SelectedSkillSlots = selectedSkills
        };
    }

    public override void OnSnapshotUpdated(GameSessionSnapshot snapshot)
    {
        base.OnSnapshotUpdated(snapshot);
        RefreshSkills();
    }

    private void RefreshSkills()
    {
        if (ActiveAccount == null || !ActiveAccount.IsRunning) return;

        var skills = GameEngine.GetSkills(ActiveAccount.Id);
        if (skills.Count == 0) return;

        foreach (var skill in skills)
        {
            if (skill.Slot >= 0 && skill.Slot < BossSkills.Count)
            {
                BossSkills[skill.Slot].SyncFrom(skill, _gameImageService, syncSelection: false);
            }
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var item in BossSkills)
        {
            item.UpdateCooldown(now);
        }
    }

    [RelayCommand]
    private void ToggleAutoBoss()
    {
        if (IsRunning)
        {
            StopAutoBoss();
        }
        else
        {
            StartAutoBoss();
        }
    }

    private void StartAutoBoss()
    {
        var targets = GetTargetAccountIds();
        if (targets.Count == 0)
        {
            StatusText = "Không có tài khoản nào đang chạy để bật.";
            return;
        }

        IsRunning = true;
        StatusText = targets.Count > 1 ? $"Đang khởi động ({targets.Count} tài khoản)..." : "Đang khởi động...";

        var options = BuildCurrentOptions();
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

            GameEngine.StartBehavior<AutoBossBehavior, AutoBossOptions>(id, options);
        }
    }

    private void StopAutoBoss()
    {
        var targets = GetTargetAccountIds();
        foreach (var id in targets)
        {
            GameEngine.StopBehavior<AutoBossBehavior>(id);
            var targetAcc = ResolveAccount(id);
            if (targetAcc != null)
            {
                var state = targetAcc.GetBehaviorState(BehaviorId);
                state.IsRunning = false;
                state.StatusText = "Đã dừng tự động săn Boss.";
            }
        }
        IsRunning = false;
        StatusText = "Đã dừng tự động săn Boss.";
    }

    [RelayCommand]
    private void SelectCurrentMap()
    {
        if (ActiveAccount?.MapId is { } mapId and >= 0)
        {
            var map = _allMaps.FirstOrDefault(m => m.MapId == mapId);
            if (map != null)
            {
                map.IsSelected = true;
            }
        }
    }

    [RelayCommand]
    private void ClearAllMaps()
    {
        _isBulkUpdating = true;
        try
        {
            foreach (var map in _allMaps)
            {
                map.IsSelected = false;
            }
            SelectedMapCount = 0;
        }
        finally
        {
            _isBulkUpdating = false;
        }
        OnOptionChanged();
    }

    [RelayCommand]
    private void SelectAllFilteredMaps()
    {
        _isBulkUpdating = true;
        try
        {
            foreach (var map in FilteredMaps)
            {
                map.IsSelected = true;
            }
            SelectedMapCount = _allMaps.Count(m => m.IsSelected);
        }
        finally
        {
            _isBulkUpdating = false;
        }
        OnOptionChanged();
    }

    [RelayCommand]
    private void SelectAllSkills()
    {
        foreach (var skill in BossSkills)
        {
            skill.IsSelected = true;
        }
    }

    [RelayCommand]
    private void DeselectAllSkills()
    {
        foreach (var skill in BossSkills)
        {
            skill.IsSelected = false;
        }
    }
}
