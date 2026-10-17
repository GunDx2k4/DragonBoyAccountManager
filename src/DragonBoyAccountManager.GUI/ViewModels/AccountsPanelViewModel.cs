using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.Dialog;
using DragonBoyAccountManager.GUI.Services.GameEngine;
using DragonBoyAccountManager.GUI.Services.Settings;
using DragonBoyAccountManager.GUI.Services.Storage;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AccountsPanelViewModel : ObservableObject
{
    private readonly IAccountStorageService _storageService;
    private readonly IGameEngineService _gameEngine;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;

    public ObservableCollection<AccountItemViewModel> Accounts { get; } = [];
    public ICollectionView AccountsView { get; }

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
    [NotifyPropertyChangedFor(nameof(IsMasked))]
    private bool _isHideAccounts;

    public bool IsMasked
    {
        get => IsHideAccounts;
        set => IsHideAccounts = value;
    }

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
        ISettingsService settingsService)
    {
        _storageService = storageService;
        _gameEngine = gameEngine;
        _dialogService = dialogService;
        _settingsService = settingsService;

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
                        AccountsView.Refresh();
                    }
                    OnStatusMessageUpdated?.Invoke($"[{item?.Username ?? "Tài khoản"}]: {message}");
                });
            }
        };
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
                IsMasked = IsHideAccounts
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
        OnAccountSelectionChanged?.Invoke(value);
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
        IsHideAccounts = !IsHideAccounts;
        foreach (var acc in Accounts)
        {
            acc.IsMasked = IsHideAccounts;
        }
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

    [RelayCommand]
    public async Task LaunchSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng chọn ít nhất một tài khoản để mở game!");
            return;
        }

        string gamePath = _settingsService.Current.GamePath;
        if (string.IsNullOrWhiteSpace(gamePath) || !File.Exists(gamePath))
        {
            _dialogService.ShowWarning("Chưa cài đặt đường dẫn game", "Vui lòng chọn tệp game (.exe) hợp lệ trước khi chạy!");
            return;
        }

        var toLaunch = targets.Where(a => !a.IsRunning).ToList();
        if (toLaunch.Count == 0)
        {
            OnStatusMessageUpdated?.Invoke("Tất cả tài khoản được chọn đều đang mở game!");
            return;
        }

        int launched = 0;
        for (int i = 0; i < toLaunch.Count; i++)
        {
            var item = toLaunch[i];
            OnStatusMessageUpdated?.Invoke($"Đang mở game ({i + 1}/{toLaunch.Count}): {item.Username}...");
            bool ok = await _gameEngine.StartAsync(item.Model, gamePath);
            if (ok) launched++;

            if (i < toLaunch.Count - 1)
            {
                await Task.Delay(Math.Max(1, _settingsService.Current.ReconnectDelaySeconds) * 1000);
            }
        }

        OnStatusMessageUpdated?.Invoke($"Đã mở thành công {launched}/{toLaunch.Count} tài khoản game.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task LoginSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng chọn ít nhất một tài khoản để đăng nhập!");
            return;
        }

        string gamePath = _settingsService.Current.GamePath;
        if (string.IsNullOrWhiteSpace(gamePath) || !File.Exists(gamePath))
        {
            _dialogService.ShowWarning("Chưa cài đặt đường dẫn game", "Vui lòng chọn tệp game (.exe) hợp lệ trước khi chạy!");
            return;
        }

        int successCount = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            var item = targets[i];
            if (string.IsNullOrWhiteSpace(item.Username) || string.IsNullOrWhiteSpace(item.Password))
            {
                OnStatusMessageUpdated?.Invoke($"Bỏ qua '{item.Username}': Thiếu tên tài khoản hoặc mật khẩu.");
                continue;
            }

            OnStatusMessageUpdated?.Invoke($"Đang xử lý đăng nhập ({i + 1}/{targets.Count}): {item.Username}...");

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
                OnStatusMessageUpdated?.Invoke($"[{item.Username}]: Đăng nhập thành công!");
            }
            else
            {
                OnStatusMessageUpdated?.Invoke($"[{item.Username}]: Đăng nhập không thành công.");
            }

            if (i < targets.Count - 1)
            {
                await Task.Delay(Math.Max(1, _settingsService.Current.ReconnectDelaySeconds) * 1000);
            }
        }

        OnStatusMessageUpdated?.Invoke($"Hoàn tất: {successCount}/{targets.Count} tài khoản đăng nhập thành công.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task StopSelectedAccountsAsync(AccountItemViewModel? target = null)
    {
        var targets = GetTargetAccounts(target);
        if (targets.Count == 0)
        {
            _dialogService.ShowWarning("Thông báo", "Vui lòng chọn ít nhất một tài khoản để đóng game!");
            return;
        }

        var running = targets.Where(a => a.IsRunning).ToList();
        if (running.Count == 0)
        {
            OnStatusMessageUpdated?.Invoke("Không có tài khoản nào đang chạy trong danh sách đã chọn.");
            return;
        }

        int stopped = 0;
        foreach (var item in running)
        {
            await _gameEngine.StopAsync(item.Id);
            stopped++;
        }

        OnStatusMessageUpdated?.Invoke($"Đã đóng {stopped} tài khoản game.");
        NotifyCountsChanged();
    }

    [RelayCommand]
    public async Task DeleteAccountAsync(AccountItemViewModel? target = null)
    {
        var item = target ?? SelectedAccount;
        if (item == null) return;

        if (item.IsRunning)
        {
            _dialogService.ShowWarning("Không thể xóa", $"Tài khoản '{item.Username}' đang chạy game. Vui lòng đóng game trước khi xóa!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Xác nhận xóa", $"Bạn có chắc chắn muốn xóa tài khoản '{item.Username}'?");
        if (!confirm) return;

        await _storageService.DeleteAsync(item.Id);
        Accounts.Remove(item);
        if (SelectedAccount == item) SelectedAccount = null;

        NotifyCountsChanged();
        AccountsView.Refresh();
        OnStatusMessageUpdated?.Invoke($"Đã xóa tài khoản {item.Username}.");
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
            _dialogService.ShowWarning("Không thể xóa", $"Có {running.Count} tài khoản đang chạy game ({names}). Vui lòng đóng game trước khi xóa!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Xác nhận xóa", $"Bạn có chắc chắn muốn xóa {targets.Count} tài khoản đã chọn?");
        if (!confirm) return;

        foreach (var item in targets)
        {
            await _storageService.DeleteAsync(item.Id);
            Accounts.Remove(item);
        }

        SelectedAccount = null;
        NotifyCountsChanged();
        AccountsView.Refresh();
        OnStatusMessageUpdated?.Invoke($"Đã xóa {targets.Count} tài khoản.");
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
            OnStatusMessageUpdated?.Invoke("Không có tài khoản nào đang mở game để sắp xếp!");
            _dialogService.ShowInfo("Sắp xếp cửa sổ", "Hiện tại không có tài khoản nào đang mở game.");
            return;
        }

        var selectedRunning = running.Where(a => a.IsSelected).ToList();
        var targets = selectedRunning.Count > 1 ? selectedRunning : running;

        _gameEngine.ArrangeWindows(targets.Select(a => a.Id));
        OnStatusMessageUpdated?.Invoke($"Đã sắp xếp {targets.Count} cửa sổ game.");
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
                IsMasked = IsHideAccounts
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
