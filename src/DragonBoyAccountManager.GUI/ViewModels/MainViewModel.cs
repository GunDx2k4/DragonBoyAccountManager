using CommunityToolkit.Mvvm.ComponentModel;

using DragonBoyAccountManager.GUI.Services.Dialog;
using DragonBoyAccountManager.GUI.Services.Storage;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAccountStorageService _storageService;
    private readonly IDialogService _dialogService;

    public AccountsPanelViewModel AccountsPanel { get; }
    public AccountFormViewModel AccountForm { get; }
    public AppStatusBarViewModel StatusBar { get; }
    public SettingsTabViewModel Settings { get; }
    public GuideViewModel Guide { get; }
    public UtilitiesTabViewModel UtilitiesTab { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    public MainViewModel(
        AccountsPanelViewModel accountsPanel,
        AccountFormViewModel accountForm,
        AppStatusBarViewModel statusBar,
        SettingsTabViewModel settings,
        GuideViewModel guide,
        UtilitiesTabViewModel utilitiesTab,
        IAccountStorageService storageService,
        IDialogService dialogService)
    {
        AccountsPanel = accountsPanel;
        AccountForm = accountForm;
        StatusBar = statusBar;
        Settings = settings;
        Guide = guide;
        UtilitiesTab = utilitiesTab;
        _storageService = storageService;
        _dialogService = dialogService;

        // Wire target resolution for movement/enter controls in AccountForm
        AccountForm.SetKeyTargetResolver(() =>
        {
            var targets = AccountsPanel.GetTargetAccounts();
            var running = targets.Where(a => a.IsRunning).Select(a => a.Id).ToList();
            if (running.Count > 0) return running;

            if (AccountsPanel.SelectedAccount != null && AccountsPanel.SelectedAccount.IsRunning)
            {
                return [AccountsPanel.SelectedAccount.Id];
            }

            return [];
        });

        // Wire target resolution for speed controls in AccountForm
        AccountForm.SetSpeedTargetResolver(() =>
        {
            if (AccountForm.IsApplySpeedToAll)
            {
                var running = AccountsPanel.Accounts.Where(a => a.IsRunning).Select(a => a.Id).ToList();
                if (running.Count > 0) return running;
            }

            var targets = AccountsPanel.GetTargetAccounts();
            var runningSelected = targets.Where(a => a.IsRunning).Select(a => a.Id).ToList();
            if (runningSelected.Count > 0) return runningSelected;

            if (AccountsPanel.SelectedAccount != null && AccountsPanel.SelectedAccount.IsRunning)
            {
                return [AccountsPanel.SelectedAccount.Id];
            }

            return [];
        });

        // Wire target resolution for UtilitiesTab zone change
        UtilitiesTab.SetTargetResolver(() =>
        {
            var targets = AccountsPanel.GetTargetAccounts();
            var running = targets.Where(a => a.IsRunning).Select(a => a.Id).ToList();
            if (running.Count > 0) return running;

            if (AccountsPanel.SelectedAccount != null && AccountsPanel.SelectedAccount.IsRunning)
            {
                return [AccountsPanel.SelectedAccount.Id];
            }

            return [];
        });

        // Wire selection coordination between accounts panel and edit form / utilities tab
        AccountsPanel.OnAccountSelectionChanged += selected =>
        {
            AccountForm.SetEditingAccount(selected);
            UtilitiesTab.SetActiveAccount(selected);
            UtilitiesTab.UpdateSyncTargetDisplay();
        };

        // When switching to Utilities tab, trigger an immediate refresh
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedTabIndex) && SelectedTabIndex == 1)
            {
                UtilitiesTab.RefreshMapData();
                UtilitiesTab.UpdateSyncTargetDisplay();
            }
        };

        AccountsPanel.OnStatusMessageUpdated += msg =>
        {
            StatusBar.StatusMessage = msg;
        };

        // Re-validate game version whenever GamePath changes
        StatusBar.GamePathChanged += _ =>
        {
            Guide.CheckAssemblyStatus();
        };

        // Sync hide account status across list and form
        AccountsPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountsPanelViewModel.IsHideAccounts))
            {
                AccountForm.IsMasked = AccountsPanel.IsHideAccounts;
            }
            else if (e.PropertyName == nameof(AccountsPanelViewModel.SelectedAccountsCount))
            {
                UtilitiesTab.UpdateSyncTargetDisplay();
            }
        };

        AccountForm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountFormViewModel.IsMasked))
            {
                if (AccountsPanel.IsHideAccounts != AccountForm.IsMasked)
                {
                    AccountsPanel.IsHideAccounts = AccountForm.IsMasked;
                    foreach (var acc in AccountsPanel.Accounts)
                    {
                        acc.IsMasked = AccountForm.IsMasked;
                    }
                }
            }
        };

        AccountForm.OnAccountSaved += async account =>
        {
            try
            {
                await _storageService.AddOrUpdateAsync(account);
                AccountsPanel.AddOrUpdateItem(account);
                StatusBar.StatusMessage = $"Đã lưu tài khoản: {account.Username}";
                _dialogService.ShowInfo("Thành công", $"Đã lưu tài khoản '{account.Username}' thành công!");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Lỗi", $"Không thể lưu tài khoản: {ex.Message}");
            }
        };

        AccountForm.OnAccountDeleted += async id =>
        {
            try
            {
                await _storageService.DeleteAsync(id);
                AccountsPanel.RemoveItem(id);
                StatusBar.StatusMessage = "Đã xóa tài khoản.";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Lỗi", $"Không thể xóa tài khoản: {ex.Message}");
            }
        };

        // Load data on startup
        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        await AccountsPanel.LoadAccountsAsync();
        StatusBar.StatusMessage = $"Sẵn sàng ({AccountsPanel.TotalAccountsCount} tài khoản)";
    }
}
