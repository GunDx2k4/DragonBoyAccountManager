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

        UtilitiesTab.SetSpeedTargetResolver(() =>
        {
            if (UtilitiesTab.IsApplySpeedToAll)
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

        AccountsPanel.OnAccountSelectionChanged += selected =>
        {
            AccountForm.SetEditingAccount(selected);
            UtilitiesTab.SetActiveAccount(selected);
            UtilitiesTab.UpdateSyncTargetDisplay();
        };

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedTabIndex))
            {
                if (SelectedTabIndex == 0)
                {
                    UtilitiesTab.StopPolling();
                    AccountForm.RefreshSkills();
                    AccountForm.RequestZoneList();
                }
                else if (SelectedTabIndex == 1)
                {
                    UtilitiesTab.StartPolling();
                    UtilitiesTab.RefreshMapData();
                    UtilitiesTab.UpdateSyncTargetDisplay();
                }
                else
                {
                    UtilitiesTab.StopPolling();
                }
            }
        };

        if (SelectedTabIndex == 1)
        {
            UtilitiesTab.StartPolling();
        }

        AccountsPanel.OnStatusMessageUpdated += msg =>
        {
            StatusBar.StatusMessage = msg;
        };

        StatusBar.GamePathChanged += _ =>
        {
            Guide.CheckAssemblyStatus();
        };

        Guide.OnRequestNavigateToGuide += () =>
        {
            SelectedTabIndex = 3;
        };

        Guide.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GuideViewModel.HasAppUpdate) && Guide.HasAppUpdate && Guide.AvailableAppUpdate != null)
            {
                StatusBar.StatusMessage = $"★ Đã có bản cập nhật mới v{Guide.AvailableAppUpdate.Version}! Vào tab Hướng dẫn để xem chi tiết.";
            }
        };

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

        _ = InitializeAsync();
    }

    public async Task InitializeAsync()
    {
        await AccountsPanel.LoadAccountsAsync();
        StatusBar.StatusMessage = $"Sẵn sàng ({AccountsPanel.TotalAccountsCount} tài khoản)";
    }
}
