using DragonBoyAccountManager.GUI.Application.Messaging;
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
        _isMasked = _settingsService.Current.IsMasked;

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
                    OnStatusMessageUpdated?.Invoke($"[{item?.DisplayUsername ?? "Tài khoản"}]: {message}");
                });
            }
        };

        WeakReferenceMessenger.Default.Register<ClearAccountSelectionMessage>(this, (_, _) =>
        {
            SelectedAccount = null;
        });
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

    public void CheckAssemblyStatus()
    {
        ClientStatus = _envService.Status;
        IsAssemblyMissing = _envService.IsAssemblyMissing;

        if (ClientStatus == ClientStatus.GameNotFound)
        {
            AssemblyGuidanceTitle = "CHUA THI?T L?P FILE GAME G?C 2.5.0 (.EXE)";
            AssemblyGuidanceMessage = "DBAM yêu cầu phiên bản game PC gốc 2.5.0 tải tại trang chủ (ngocrongonline.com) — tuyệt đối không cài bản mod. Vui lòng bấm \"Chọn file game (.exe)\" để bắt đầu.";
        }
        else if (ClientStatus == ClientStatus.AssemblyNotFound)
        {
            AssemblyGuidanceTitle = "KHÔNG TÌM THẤY ASSEMBLY-CSHARP.DLL";
            AssemblyGuidanceMessage = "Đã nhận file .exe nhưng thiếu file Assembly-CSharp.dll trong Data\\Managed. Vui lòng tải bản game PC 2.5.0 gốc tại trang chủ (không dùng bản mod) và kiểm tra lại thư mục game.";
        }
        else if (ClientStatus == ClientStatus.ModifiedOrMod)
        {
            AssemblyGuidanceTitle = "PHÁT HIỆN FILE GAME BỊ SỬA ĐỔI HOẶC BẢN MOD";
            AssemblyGuidanceMessage = "DBAM chỉ hỗ trợ phiên bản gốc 2.5.0 chính thức từ trang chủ. Các bản mod (Mod, Auto, Hack, DLL đã can thiệp) có thể gây lỗi bộ nhớ hoặc không tương thích!";
        }
        else if (ClientStatus == ClientStatus.OutdatedVersion)
        {
            AssemblyGuidanceTitle = "PHIÊN BẢN GAME ĐÃ CŨ";
            AssemblyGuidanceMessage = "Phiên bản game hiện tại không phải 2.5.0. Vui lòng tải bản mới nhất tại ngocrongonline.com để sử dụng.";
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
            "Game Executable (*.exe)|*.exe|Tất cả tệp (*.*)|*.*",
            initialDir);

        if (!string.IsNullOrEmpty(selectedFile))
        {
            if (!string.Equals(Path.GetExtension(selectedFile), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowWarning("File không hợp lệ", "Đường dẫn game bắt buộc phải là file thực thi (.exe)!");
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
        _settingsService.Current.IsMasked = IsMasked;
        _ = _settingsService.SaveAsync();
        _gameEngine.SetMasked(IsMasked);
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
        WeakReferenceMessenger.Default.Send(new RequestNavigateToTabMessage(0));
        OnStatusMessageUpdated?.Invoke("Đang ở chế độ thêm tài khoản mới. Vui lòng nhập thông tin bên phải.");
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
                "Không tìm thấy Assembly-CSharp",
                $"Đã nhận diện file thực thi game: {Path.GetFileName(gamePath)}\n" +
                "Nhưng không tìm thấy file Assembly-CSharp.dll trong thư mục dữ liệu Data\\Managed.\n\n" +
                "Vui lòng tải bản game PC gốc 2.5.0 tại trang chủ (ngocrongonline.com) — không dùng bản mod, và đảm bảo cấu trúc:\n" +
                "- [TênGame].exe\n" +
                "- [TênGame]_Data\\Managed\\Assembly-CSharp.dll");
        }
        else
        {
            bool chooseNow = _dialogService.ShowConfirmation(
                "Chưa cài đặt đường dẫn game",
                "Chưa chọn tệp thực thi game (.exe) hoặc đường dẫn không tồn tại.\n\n" +
                "Vui lòng tải bản game PC gốc 2.5.0 tại trang chủ (ngocrongonline.com) — tuyệt đối không cài bản mod.\n\n" +
                "Bạn có muốn chọn file game (.exe) ngay bây giờ không?",
                "Ch?n file ngay",
                "Để sau");
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
            _dialogService.ShowWarning("Thông báo", "Vui lòng chọn ít nhất một tài khoản để mở game!");
            return;
        }

        if (!EnsureGameClientReady(out string gamePath))
        {
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
            OnStatusMessageUpdated?.Invoke($"Đang mở game ({i + 1}/{toLaunch.Count}): {item.DisplayUsername}...");
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
                OnStatusMessageUpdated?.Invoke($"Bỏ qua '{item.DisplayUsername}': Thiếu tên tài khoản hoặc mật khẩu.");
                continue;
            }

            OnStatusMessageUpdated?.Invoke($"Đang xử lý đăng nhập ({i + 1}/{targets.Count}): {item.DisplayUsername}...");

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
                OnStatusMessageUpdated?.Invoke($"[{item.DisplayUsername}]: Đăng nhập thành công!");
            }
            else
            {
                OnStatusMessageUpdated?.Invoke($"[{item.DisplayUsername}]: Đăng nhập không thành công.");
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
            _dialogService.ShowWarning("Không thể xóa", $"Tài khoản '{item.DisplayUsername}' đang chạy game. Vui lòng đóng game trước khi xóa!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Xác nhận xóa", $"Bạn có chắc chắn muốn xóa tài khoản '{item.DisplayUsername}'?");
        if (!confirm) return;

        await _storageService.DeleteAsync(item.Id);
        Accounts.Remove(item);
        if (SelectedAccount == item) SelectedAccount = null;

        NotifyCountsChanged();
        AccountsView.Refresh();
        OnStatusMessageUpdated?.Invoke($"Đã xóa tài khoản {item.DisplayUsername}.");
    }

    [RelayCommand]
    public async Task DeleteSelectedAccountsAsync()
    {
        var targets = GetTargetAccounts();
        if (targets.Count == 0) return;

        var running = targets.Where(a => a.IsRunning).ToList();
        if (running.Count > 0)
        {
            var names = string.Join(", ", running.Select(a => $"'{a.DisplayUsername}'"));
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
