using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Accounts;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Sessions.Validation;
using DragonBoyAccountManager.GUI.Domain.Messaging;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Services.Dialogs;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Accounts;

public sealed partial class AccountsPanelViewModel : ObservableObject
{
    private readonly IAccountStorageService _storageService;
    private readonly IGameEngineService _gameEngine;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly IGameEnvironmentService _envService;
    private readonly IActiveSelectionService? _selectionService;
    private readonly IDispatcherService? _dispatcher;

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];
    public ICollectionView AccountsView { get; }

    [ObservableProperty]
    private ClientStatus _clientStatus = ClientStatus.GameNotFound;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuidanceVisible))]
    [NotifyPropertyChangedFor(nameof(ShowGuidanceButtonVisible))]
    private bool _isAssemblyMissing = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuidanceVisible))]
    [NotifyPropertyChangedFor(nameof(ShowGuidanceButtonVisible))]
    private bool _isGuidanceDismissed;

    [ObservableProperty]
    private string _assemblyGuidanceTitle = string.Empty;

    [ObservableProperty]
    private string _assemblyGuidanceMessage = string.Empty;

    public bool IsGuidanceVisible => IsAssemblyMissing && !IsGuidanceDismissed;
    public bool ShowGuidanceButtonVisible => IsAssemblyMissing && IsGuidanceDismissed;

    [ObservableProperty]
    private AccountItemViewModel? _selectedAccount;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _selectedServerFilter = "Tất cả";

    [ObservableProperty]
    private bool _isAllSelected;

    private bool _isUpdatingSelection;

    [ObservableProperty]
    private bool _isMasked;

    partial void OnIsMaskedChanged(bool value)
    {
        foreach (var acc in Accounts)
        {
            acc.IsMasked = value;
        }
    }

    public ObservableCollection<string> FilterServers { get; } = [];

    private readonly List<AccountItemViewModel> _selectedRows = [];
    public IReadOnlyList<AccountItemViewModel> SelectedRows => _selectedRows;

    public void UpdateSelectedRows(IEnumerable<AccountItemViewModel> items)
    {
        _selectedRows.Clear();
        _selectedRows.AddRange(items);
        NotifyCountsChanged();
    }

    public int TotalAccountsCount => Accounts.Count;
    public int RunningAccountsCount => Accounts.Count(a => a.IsRunning);
    public int SelectedAccountsCount
    {
        get
        {
            int checkedCount = Accounts.Count(a => a.IsSelected);
            if (checkedCount > 0) return checkedCount;
            if (_selectedRows.Count > 0) return _selectedRows.Count;
            return SelectedAccount != null ? 1 : 0;
        }
    }

    public bool CanDeleteSelectedAccount
    {
        get
        {
            var targets = GetTargetAccounts();
            if (targets.Count > 0) return targets.All(a => !a.IsRunning && !_gameEngine.IsRunning(a.Id));
            return SelectedAccount != null && !SelectedAccount.IsRunning && !_gameEngine.IsRunning(SelectedAccount.Id);
        }
    }

    public bool CanControlAccount
    {
        get
        {
            var targets = GetTargetAccounts();
            if (targets.Count > 0) return targets.Any(a => a.IsRunning);
            return SelectedAccount != null && SelectedAccount.IsRunning;
        }
    }

    public event Action<string>? OnStatusMessageUpdated;

    public AccountsPanelViewModel(
        IAccountStorageService storageService,
        IGameEngineService gameEngine,
        IDialogService dialogService,
        ISettingsService settingsService,
        IGameEnvironmentService envService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
    {
        _storageService = storageService;
        _gameEngine = gameEngine;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _envService = envService;
        _selectionService = selectionService;
        _dispatcher = dispatcher;
        _isMasked = _settingsService.Current.IsMasked;

        _selectionService?.RegisterAccountsProvider(
            () => Accounts,
            () => GetTargetAccounts());

        _envService.EnvironmentChanged += CheckAssemblyStatus;
        CheckAssemblyStatus();

        AccountsView = CollectionViewSource.GetDefaultView(Accounts);
        AccountsView.Filter = FilterAccounts;

        InitServerFilters();

        _gameEngine.OnSessionEvent += HandleSessionEvent;

        WeakReferenceMessenger.Default.Register<ClearAccountSelectionMessage>(this, (_, _) =>
        {
            SelectedAccount = null;
        });

        WeakReferenceMessenger.Default.Register<MaskingStateChangedMessage>(this, (_, m) =>
        {
            if (IsMasked != m.IsMasked)
            {
                IsMasked = m.IsMasked;
            }
        });
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher != null)
        {
            _ = _dispatcher.InvokeAsync(action);
        }
        else if (App.Current?.Dispatcher != null)
        {
            _ = App.Current.Dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    private void NotifyStatusMessage(string message)
    {
        OnStatusMessageUpdated?.Invoke(message);
        WeakReferenceMessenger.Default.Send(new StatusMessageChangedMessage(message));
    }

    private void HandleSessionEvent(SessionEvent evt)
    {
        switch (evt)
        {
            case SessionStateChangedEvent s:
                RunOnUi(() =>
                {
                    var item = Accounts.FirstOrDefault(a => a.Id == s.AccountId);
                    if (item != null)
                    {
                        item.UpdateStatus(s.Status, _gameEngine.GetProcessId(s.AccountId));
                        if (s.Snapshot != null)
                        {
                            item.UpdateSnapshot(s.Snapshot);
                        }
                        NotifyCountsChanged();
                    }
                });
                break;

            case SessionMessageEvent m:
                RunOnUi(() =>
                {
                    var item = Accounts.FirstOrDefault(a => a.Id == m.AccountId);
                    if (item != null)
                    {
                        item.StatusMessage = m.Message;
                    }
                    NotifyStatusMessage($"[{item?.DisplayUsername ?? "Tài khoản"}]: {m.Message}");
                });
                break;

            case ActionReportedEvent a:
                RunOnUi(() =>
                {
                    var item = Accounts.FirstOrDefault(acc => acc.Id == a.AccountId);
                    item?.UpdateActionReport(a.Report);
                });
                break;
        }
    }

    private void InitServerFilters()
    {
        FilterServers.Clear();
        FilterServers.Add("Tất cả");
        foreach (var server in ServerOption.DefaultServers)
        {
            FilterServers.Add(server.ServerName);
        }
    }

    [RelayCommand]
    public async Task LoadAccountsAsync()
    {
        var list = await _storageService.LoadAllAsync();
        foreach (var old in Accounts)
        {
            old.PropertyChanged -= OnAccountItemPropertyChanged;
        }
        Accounts.Clear();
        foreach (var acc in list)
        {
            var vm = new AccountItemViewModel(acc)
            {
                IsMasked = IsMasked
            };
            vm.PropertyChanged += OnAccountItemPropertyChanged;
            Accounts.Add(vm);
        }

        SyncIsAllSelectedState();
        NotifyCountsChanged();
        AccountsView.Refresh();
    }

    private void OnAccountItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountItemViewModel.IsSelected))
        {
            SyncIsAllSelectedState();
            NotifyCountsChanged();
        }
        else if (e.PropertyName == nameof(AccountItemViewModel.IsRunning))
        {
            NotifyCountsChanged();
        }
    }

    private void SyncIsAllSelectedState()
    {
        if (_isUpdatingSelection) return;

        var visibleAccounts = AccountsView.Cast<AccountItemViewModel>().ToList();
        if (visibleAccounts.Count == 0)
        {
            SetIsAllSelectedSilently(false);
            return;
        }

        bool allSelected = visibleAccounts.All(a => a.IsSelected);
        SetIsAllSelectedSilently(allSelected);
    }

    private void SetIsAllSelectedSilently(bool value)
    {
        if (IsAllSelected != value)
        {
            _isUpdatingSelection = true;
            try
            {
                IsAllSelected = value;
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }
    }

    partial void OnSelectedAccountChanged(AccountItemViewModel? value)
    {
        if (_selectionService != null)
        {
            _selectionService.SelectedAccount = value;
        }
        NotifyCountsChanged();
    }

    partial void OnSearchKeywordChanged(string value)
    {
        AccountsView.Refresh();
        SyncIsAllSelectedState();
    }

    partial void OnSelectedServerFilterChanged(string value)
    {
        AccountsView.Refresh();
        SyncIsAllSelectedState();
    }

    partial void OnIsAllSelectedChanged(bool value)
    {
        if (_isUpdatingSelection) return;

        _isUpdatingSelection = true;
        try
        {
            var visibleAccounts = AccountsView.Cast<AccountItemViewModel>().ToList();
            foreach (var item in visibleAccounts)
            {
                item.IsSelected = value;
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }

        NotifyCountsChanged();
    }

    [RelayCommand]
    public void ToggleSelectAllAccounts()
    {
        IsAllSelected = !IsAllSelected;
    }

    [RelayCommand]
    public void ToggleHideAccounts()
    {
        IsMasked = !IsMasked;
        _settingsService.Current.IsMasked = IsMasked;
        _ = _settingsService.SaveAsync();
        _gameEngine.SetMasked(IsMasked);
        WeakReferenceMessenger.Default.Send(new MaskingStateChangedMessage(IsMasked));
    }

    [RelayCommand]
    public void ClearFilter()
    {
        SearchKeyword = string.Empty;
        SelectedServerFilter = "Tất cả";
        AccountsView.Refresh();
        SyncIsAllSelectedState();
    }

    [RelayCommand]
    public void OpenAddAccount()
    {
        SelectedAccount = null;
        WeakReferenceMessenger.Default.Send(new RequestNavigateToTabMessage(AppTab.AccountConfig));
        NotifyStatusMessage("Đang ở chế độ thêm tài khoản mới. Vui lòng nhập thông tin bên phải.");
    }

    private bool FilterAccounts(object item)
    {
        if (item is not AccountItemViewModel acc) return false;

        if (SelectedServerFilter != "Tất cả" && !string.Equals(acc.Server, SelectedServerFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(SearchKeyword))
        {
            string kw = SearchKeyword.Trim();
            bool matchUser = acc.Username.Contains(kw, StringComparison.OrdinalIgnoreCase);
            bool matchChar = !string.IsNullOrEmpty(acc.CharacterName) && acc.CharacterName.Contains(kw, StringComparison.OrdinalIgnoreCase);
            bool matchMap = !string.IsNullOrEmpty(acc.MapName) && acc.MapName.Contains(kw, StringComparison.OrdinalIgnoreCase);
            return matchUser || matchChar || matchMap;
        }

        return true;
    }

    public void NotifyCountsChanged()
    {
        OnPropertyChanged(nameof(TotalAccountsCount));
        OnPropertyChanged(nameof(RunningAccountsCount));
        OnPropertyChanged(nameof(SelectedAccountsCount));
        OnPropertyChanged(nameof(CanDeleteSelectedAccount));
        OnPropertyChanged(nameof(CanControlAccount));
        _selectionService?.NotifyTargetsChanged();
    }

    public List<AccountItemViewModel> GetTargetAccounts(AccountItemViewModel? specific = null)
    {
        if (specific != null)
        {
            var checkedItems = Accounts.Where(a => a.IsSelected).ToList();
            if (checkedItems.Contains(specific)) return checkedItems;

            if (_selectedRows.Contains(specific) && _selectedRows.Count > 1)
                return [.. _selectedRows];

            return [specific];
        }

        var selected = Accounts.Where(a => a.IsSelected).ToList();
        if (selected.Count > 0) return selected;

        if (_selectedRows.Count > 0) return [.. _selectedRows];

        if (SelectedAccount != null) return [SelectedAccount];

        return [];
    }

    public void AddOrUpdateItem(Account account)
    {
        var existing = Accounts.FirstOrDefault(a => a.Id == account.Id);
        if (existing != null)
        {
            existing.Model.Username = account.Username;
            existing.Model.Password = account.Password;
            existing.Model.ServerName = account.ServerName;
            existing.Model.ServerId = account.ServerId;
            existing.Model.GameSpeed = account.GameSpeed;
            existing.Model.AutoLogin = account.AutoLogin;
            existing.RefreshData();
        }
        else
        {
            var newItem = new AccountItemViewModel(account)
            {
                IsMasked = IsMasked
            };
            newItem.PropertyChanged += OnAccountItemPropertyChanged;
            Accounts.Add(newItem);
        }

        SyncIsAllSelectedState();
        NotifyCountsChanged();
        AccountsView.Refresh();
    }

    public void RemoveItem(Guid id)
    {
        var existing = Accounts.FirstOrDefault(a => a.Id == id);
        if (existing != null)
        {
            existing.PropertyChanged -= OnAccountItemPropertyChanged;
            if (SelectedAccount == existing)
            {
                SelectedAccount = null;
            }
            Accounts.Remove(existing);
            SyncIsAllSelectedState();
            NotifyCountsChanged();
            AccountsView.Refresh();
        }
    }
}
