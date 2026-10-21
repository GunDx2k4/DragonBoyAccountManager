using CommunityToolkit.Mvvm.Messaging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AccountsPanelViewModel : ObservableObject
{
    private readonly IAccountStorageService _storageService;
    private readonly IGameEngineService _gameEngine;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly IGameEnvironmentService _envService;
    private readonly IActiveSelectionService? _selectionService;

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

    public event Action? OnRequestNavigateToGuide;
    public event Action<string>? OnGamePathConfigured;

    [ObservableProperty]
    private AccountItemViewModel? _selectedAccount;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _selectedServerFilter = "T?t c?";

    [ObservableProperty]
    private bool _isAllSelected;

    private bool _isUpdatingSelection;

    [ObservableProperty]
    private bool _isMasked;

    [ObservableProperty]
    private bool _isApplySpeedToAll = true;

    [ObservableProperty]
    private float _globalGameSpeed = 1.0f;

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
            if (targets.Count > 0) return targets.All(a => !a.IsRunning);
            return SelectedAccount != null && !SelectedAccount.IsRunning;
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

    public event Action<AccountItemViewModel?>? OnAccountSelectionChanged;
    public event Action<string>? OnStatusMessageUpdated;

    public AccountsPanelViewModel(
        IAccountStorageService storageService,
        IGameEngineService gameEngine,
        IDialogService dialogService,
        ISettingsService settingsService,
        IGameEnvironmentService envService,
        IActiveSelectionService? selectionService = null)
    {
        _storageService = storageService;
        _gameEngine = gameEngine;
        _dialogService = dialogService;
        _settingsService = settingsService;
        _envService = envService;
        _selectionService = selectionService;

        _selectionService?.RegisterAccountsProvider(
            () => Accounts,
            () => GetTargetAccounts());

        _envService.EnvironmentChanged += CheckAssemblyStatus;
        CheckAssemblyStatus();

        AccountsView = CollectionViewSource.GetDefaultView(Accounts);
        AccountsView.Filter = FilterAccounts;

        InitServerFilters();

        _gameEngine.OnSessionUpdated += (accountId, status, snapshot) =>
        {
            var dispatcher = App.Current?.Dispatcher;
            if (dispatcher != null)
            {
                _ = dispatcher.InvokeAsync(() =>
                {
                    var item = Accounts.FirstOrDefault(a => a.Id == accountId);
                    if (item != null)
                    {
                        item.UpdateStatus(status, _gameEngine.GetProcessId(accountId));
                        if (snapshot != null)
                        {
                            item.UpdateSnapshot(snapshot);
                        }
                        NotifyCountsChanged();
                    }
                });
            }
        };

        _gameEngine.OnSessionMessageUpdated += (accountId, message) =>
        {
            var dispatcher = App.Current?.Dispatcher;
            if (dispatcher != null)
            {
                _ = dispatcher.InvokeAsync(() =>
                {
                    var item = Accounts.FirstOrDefault(a => a.Id == accountId);
                    if (item != null)
                    {
                        item.StatusMessage = message;
                    }
                    OnStatusMessageUpdated?.Invoke($"[{item?.Username ?? "Tài kho?n"}]: {message}");
                });
            }
        };
    }

    private void InitServerFilters()
    {
        FilterServers.Clear();
        FilterServers.Add("T?t c?");
        foreach (var server in ServerOption.DefaultServers)
        {
            FilterServers.Add(server.ServerName);
        }
    }

    public void CheckAssemblyStatus()
    {
        ClientStatus = _envService.Status;
        IsAssemblyMissing = _envService.IsAssemblyMissing;

        if (ClientStatus == ClientStatus.GameNotFound)
        {
            AssemblyGuidanceTitle = "CHUA THI?T L?P FILE GAME G?C 2.5.0 (.EXE)";
            AssemblyGuidanceMessage = "DBAM yêu c?u phiên b?n game PC g?c 2.5.0 t?i t?i trang ch? (ngocrongonline.com) — tuy?t d?i không cài b?n mod. Vui lòng b?m \"Ch?n file game (.exe)\" d? b?t d?u.";
        }
        else if (ClientStatus == ClientStatus.AssemblyNotFound)
        {
            AssemblyGuidanceTitle = "KHÔNG TÌM TH?Y ASSEMBLY-CSHARP.DLL";
            AssemblyGuidanceMessage = "Ðã nh?n file .exe nhung thi?u file Assembly-CSharp.dll trong Data\\Managed. Vui lòng t?i b?n game PC 2.5.0 g?c t?i trang ch? (không dùng b?n mod) và ki?m tra l?i thu m?c game.";
        }
        else if (ClientStatus == ClientStatus.ModifiedOrMod)
        {
            AssemblyGuidanceTitle = "PHÁT HI?N FILE GAME B? S?A Ð?I HO?C B?N MOD";
            AssemblyGuidanceMessage = "DBAM ch? h? tr? phiên b?n g?c 2.5.0 chính th?c t? trang ch?. Các b?n mod (Mod, Auto, Hack, DLL dã can thi?p) có th? gây l?i b? nh? ho?c không tuong thích!";
        }
        else if (ClientStatus == ClientStatus.OutdatedVersion)
        {
            AssemblyGuidanceTitle = "PHIÊN B?N GAME ÐÃ CU";
            AssemblyGuidanceMessage = "Phiên b?n game hi?n t?i không ph?i 2.5.0. Vui lòng t?i b?n m?i nh?t t?i ngocrongonline.com d? s? d?ng.";
        }
        else
        {
            AssemblyGuidanceTitle = string.Empty;
            AssemblyGuidanceMessage = string.Empty;
            IsGuidanceDismissed = false;
        }
    }

    [RelayCommand]
    public void BrowseGamePath()
    {
        var initialDir = !string.IsNullOrEmpty(_envService.GamePath)
            ? Path.GetDirectoryName(_envService.GamePath)
            : null;

        var selectedFile = _dialogService.ShowOpenFileDialog(
            "Ch?n t?p th?c thi game Dragon Boy",
            "Game Executable (*.exe)|*.exe|T?t c? t?p (*.*)|*.*",
            initialDir);

        if (!string.IsNullOrEmpty(selectedFile))
        {
            if (!string.Equals(Path.GetExtension(selectedFile), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowWarning("File không h?p l?", "Ðu?ng d?n game b?t bu?c ph?i là file th?c thi (.exe)!");
                return;
            }

            _envService.GamePath = selectedFile;
            OnGamePathConfigured?.Invoke(selectedFile);
        }
    }

    [RelayCommand]
    public void OpenGuide()
    {
        OnRequestNavigateToGuide?.Invoke();
    }

    [RelayCommand]
    public void DismissGuidance()
    {
        IsGuidanceDismissed = true;
    }

    [RelayCommand]
    public void ShowGuidance()
    {
        IsGuidanceDismissed = false;
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

    private void OnAccountItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
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
        if (_selectionService != null) _selectionService.SelectedAccount = value;
        OnAccountSelectionChanged?.Invoke(value);
        WeakReferenceMessenger.Default.Send(new AccountSelectedMessage(value));
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
        foreach (var acc in Accounts)
        {
            acc.IsMasked = IsMasked;
        }
    }

    [RelayCommand]
    public void ClearFilter()
    {
        SearchKeyword = string.Empty;
        SelectedServerFilter = "T?t c?";
        AccountsView.Refresh();
        SyncIsAllSelectedState();
    }

    [RelayCommand]
    public void OpenAddAccount()
    {
        SelectedAccount = null;
    }

    private bool FilterAccounts(object item)
    {
        if (item is not AccountItemViewModel acc) return false;

        if (SelectedServerFilter != "T?t c?" && !string.Equals(acc.Server, SelectedServerFilter, StringComparison.OrdinalIgnoreCase))
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
        WeakReferenceMessenger.Default.Send(new TargetAccountsChangedMessage(GetTargetAccounts().Select(a => a.Id).ToList()));
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

    [RelayCommand]
    public async Task OpenOrFocusAccountAsync(AccountItemViewModel? item)
    {
        item ??= SelectedAccount;
        if (item == null) return;

        if (item.IsRunning && item.Status == GameSessionStatus.Active)
        {
            FocusGameWindow(item);
        }
        else
        {
            await LoginSelectedAccountsCommand.ExecuteAsync(item);
        }
    }

    private bool EnsureGameClientReady(out string gamePath)
    {
        gamePath = _envService.GamePath;
        var metadata = _envService.CurrentMetadata;
        if (metadata != null && metadata.IsFound)
        {
            return true;
        }

        if (metadata?.Status == ClientStatus.AssemblyNotFound)
        {
            _dialogService.ShowWarning(
                "Không tìm th?y Assembly-CSharp",
                $"Ðã nh?n di?n file th?c thi game: {Path.GetFileName(gamePath)}\n" +
                "Nhung không tìm th?y file Assembly-CSharp.dll trong thu m?c d? li?u Data\\Managed.\n\n" +
                "Vui lòng t?i b?n game PC g?c 2.5.0 t?i trang ch? (ngocrongonline.com) — không dùng b?n mod, và d?m b?o c?u trúc:\n" +
                "- [TênGame].exe\n" +
                "- [TênGame]_Data\\Managed\\Assembly-CSharp.dll");
        }
        else
        {
            bool chooseNow = _dialogService.ShowConfirmation(
                "Chua cài d?t du?ng d?n game",
                "Chua ch?n t?p th?c thi game (.exe) ho?c du?ng d?n không t?n t?i.\n\n" +
                "Vui lòng t?i b?n game PC g?c 2.5.0 t?i trang ch? (ngocrongonline.com) — tuy?t d?i không cài b?n mod.\n\n" +
                "B?n có mu?n ch?n file game (.exe) ngay bây gi? không?",
                "Ch?n file ngay",
                "Ð? sau");
            if (chooseNow)
            {
                BrowseGamePath();
            }
        }

        return false;
    }

    [RelayCommand]
    public async Task LaunchSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng ch?n ít nh?t m?t tài kho?n d? m? game!");
            return;
        }

        if (!EnsureGameClientReady(out string gamePath))
        {
            return;
        }

        var toLaunch = targets.Where(a => !a.IsRunning).ToList();
        if (toLaunch.Count == 0)
        {
            OnStatusMessageUpdated?.Invoke("T?t c? tài kho?n du?c ch?n d?u dang m? game!");
            return;
        }

        int launched = 0;
        for (int i = 0; i < toLaunch.Count; i++)
        {
            var item = toLaunch[i];
            OnStatusMessageUpdated?.Invoke($"Ðang m? game ({i + 1}/{toLaunch.Count}): {item.Username}...");
            bool ok = await _gameEngine.StartAsync(item.Model, gamePath);
            if (ok) launched++;

            if (i < toLaunch.Count - 1)
            {
                await Task.Delay(Math.Max(1, _settingsService.Current.ReconnectDelaySeconds) * 1000);
            }
        }

        OnStatusMessageUpdated?.Invoke($"Ðã m? thành công {launched}/{toLaunch.Count} tài kho?n game.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task LoginSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng ch?n ít nh?t m?t tài kho?n d? dang nh?p!");
            return;
        }

        if (!EnsureGameClientReady(out string gamePath))
        {
            return;
        }

        int successCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            var item = targets[i];
            if (string.IsNullOrWhiteSpace(item.Username) || string.IsNullOrWhiteSpace(item.Password))
            {
                OnStatusMessageUpdated?.Invoke($"B? qua '{item.Username}': Thi?u tên tài kho?n ho?c m?t kh?u.");
                continue;
            }

            OnStatusMessageUpdated?.Invoke($"Ðang x? lý dang nh?p ({i + 1}/{targets.Count}): {item.Username}...");

            bool ok;
            if (!item.IsRunning)
            {
                item.Model.AutoLogin = true;
                ok = await _gameEngine.StartAsync(item.Model, gamePath);
            }
            else
            {
                ok = await _gameEngine.LoginAsync(item.Id);
            }

            if (ok)
            {
                successCount++;
                OnStatusMessageUpdated?.Invoke($"[{item.Username}]: Ðang nh?p thành công!");
            }
            else
            {
                OnStatusMessageUpdated?.Invoke($"[{item.Username}]: Ðang nh?p không thành công.");
            }

            if (i < targets.Count - 1)
            {
                await Task.Delay(Math.Max(1, _settingsService.Current.ReconnectDelaySeconds) * 1000);
            }
        }

        OnStatusMessageUpdated?.Invoke($"Hoàn t?t: {successCount}/{targets.Count} tài kho?n dang nh?p thành công.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task StopSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng ch?n ít nh?t m?t tài kho?n d? dóng game!");
            return;
        }

        var running = targets.Where(a => a.IsRunning).ToList();
        if (running.Count == 0)
        {
            OnStatusMessageUpdated?.Invoke("Không có tài kho?n nào dang ch?y trong danh sách dã ch?n.");
            return;
        }

        int stopped = 0;
        foreach (var item in running)
        {
            await _gameEngine.StopAsync(item.Id);
            stopped++;
        }

        OnStatusMessageUpdated?.Invoke($"Ðã dóng {stopped} tài kho?n game.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task DeleteAccountAsync(AccountItemViewModel? target = null)
    {
        var item = target ?? SelectedAccount;
        if (item == null) return;

        if (item.IsRunning)
        {
            _dialogService.ShowWarning("Không th? xóa", $"Tài kho?n '{item.Username}' dang ch?y game. Vui lòng dóng game tru?c khi xóa!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Xác nh?n xóa", $"B?n có ch?c ch?n mu?n xóa tài kho?n '{item.Username}'?");
        if (!confirm) return;

        await _storageService.DeleteAsync(item.Id);
        Accounts.Remove(item);
        if (SelectedAccount == item) SelectedAccount = null;

        NotifyCountsChanged();
        AccountsView.Refresh();
        OnStatusMessageUpdated?.Invoke($"Ðã xóa tài kho?n {item.Username}.");
    }

    [RelayCommand]
    public async Task DeleteSelectedAccountsAsync()
    {
        var targets = GetTargetAccounts();
        if (targets.Count == 0) return;

        var running = targets.Where(a => a.IsRunning).ToList();
        if (running.Count > 0)
        {
            var names = string.Join(", ", running.Select(a => $"'{a.Username}'"));
            _dialogService.ShowWarning("Không th? xóa", $"Có {running.Count} tài kho?n dang ch?y game ({names}). Vui lòng dóng game tru?c khi xóa!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Xác nh?n xóa", $"B?n có ch?c ch?n mu?n xóa {targets.Count} tài kho?n dã ch?n?");
        if (!confirm) return;

        foreach (var item in targets)
        {
            await _storageService.DeleteAsync(item.Id);
            Accounts.Remove(item);
        }

        SelectedAccount = null;
        NotifyCountsChanged();
        AccountsView.Refresh();
        OnStatusMessageUpdated?.Invoke($"Ðã xóa {targets.Count} tài kho?n.");
    }

    [RelayCommand]
    public void FocusGameWindow(AccountItemViewModel? target = null)
    {
        var item = target ?? SelectedAccount;
        if (item != null && item.IsRunning)
        {
            _gameEngine.FocusWindow(item.Id);
        }
    }

    [RelayCommand]
    public void ArrangeWindows()
    {
        var running = Accounts.Where(a => a.IsRunning).ToList();
        if (running.Count == 0)
        {
            OnStatusMessageUpdated?.Invoke("Không có tài kho?n nào dang m? game d? s?p x?p!");
            _dialogService.ShowInfo("S?p x?p c?a s?", "Hi?n t?i không có tài kho?n nào dang m? game.");
            return;
        }

        var selectedRunning = running.Where(a => a.IsSelected).ToList();
        var targets = selectedRunning.Count > 1 ? selectedRunning : running;

        _gameEngine.ArrangeWindows(targets.Select(a => a.Id));
        OnStatusMessageUpdated?.Invoke($"Ðã s?p x?p {targets.Count} c?a s? game.");
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
