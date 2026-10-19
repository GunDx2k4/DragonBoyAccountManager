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
    public AutoTabViewModel AutoTab { get; }
    public FeedbackViewModel Feedback { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    public MainViewModel(
        AccountsPanelViewModel accountsPanel,
        AccountFormViewModel accountForm,
        AppStatusBarViewModel statusBar,
        SettingsTabViewModel settings,
        GuideViewModel guide,
        UtilitiesTabViewModel utilitiesTab,
        AutoTabViewModel autoTab,
        FeedbackViewModel feedback,
        IAccountStorageService storageService,
        IDialogService dialogService)
    {
        AccountsPanel = accountsPanel;
        AccountForm = accountForm;
        StatusBar = statusBar;
        Settings = settings;
        Guide = guide;
        UtilitiesTab = utilitiesTab;
        AutoTab = autoTab;
        Feedback = feedback;
        _storageService = storageService;
        _dialogService = dialogService;

        AccountForm.SetKeyTargetResolver(ResolveRunningTargetAccountIds);
        AccountForm.SetSpeedTargetResolver(() => ResolveRunningSpeedTargetAccountIds(AccountForm.IsApplySpeedToAll));
        UtilitiesTab.SetTargetResolver(ResolveRunningTargetAccountIds);
        UtilitiesTab.SetSpeedTargetResolver(() => ResolveRunningSpeedTargetAccountIds(UtilitiesTab.IsApplySpeedToAll));
        AutoTab.SetTargetResolver(ResolveRunningTargetAccountIds);

        AccountsPanel.OnAccountSelectionChanged += selected =>
        {
            AccountForm.SetEditingAccount(selected);
            UtilitiesTab.SetActiveAccount(selected);
            UtilitiesTab.UpdateSyncTargetDisplay();
            AutoTab.SetActiveAccount(selected);
            AutoTab.UpdateSyncTargetDisplay();
        };

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedTabIndex))
            {
                if (SelectedTabIndex == 0)
                {
                    UtilitiesTab.StopPolling();
                    AutoTab.StopPolling();
                    AccountForm.StartPolling();
                    AccountForm.RefreshSkills();
                    AccountForm.RequestZoneList();
                }
                else if (SelectedTabIndex == 1)
                {
                    AccountForm.StopPolling();
                    AutoTab.StopPolling();
                    UtilitiesTab.StartPolling();
                    UtilitiesTab.RefreshMapData();
                    UtilitiesTab.UpdateSyncTargetDisplay();
                }
                else if (SelectedTabIndex == 2)
                {
                    AccountForm.StopPolling();
                    UtilitiesTab.StopPolling();
                    AutoTab.StartPolling();
                    AutoTab.RefreshData();
                    AutoTab.UpdateSyncTargetDisplay();
                }
                else
                {
                    AccountForm.StopPolling();
                    UtilitiesTab.StopPolling();
                    AutoTab.StopPolling();
                }
            }
        };

        if (SelectedTabIndex == 0)
        {
            AccountForm.StartPolling();
        }
        else if (SelectedTabIndex == 1)
        {
            UtilitiesTab.StartPolling();
        }
        else if (SelectedTabIndex == 2)
        {
            AutoTab.StartPolling();
        }

        AccountsPanel.OnStatusMessageUpdated += msg =>
        {
            StatusBar.StatusMessage = msg;
        };

        StatusBar.GamePathChanged += _ =>
        {
            Guide.CheckAssemblyStatus();
            AccountsPanel.CheckAssemblyStatus();
        };

        AccountsPanel.OnGamePathConfigured += path =>
        {
            StatusBar.GamePath = path;
            Guide.CheckAssemblyStatus();
        };

        AccountsPanel.OnRequestNavigateToGuide += () =>
        {
            SelectedTabIndex = 4;
        };

        Guide.OnGamePathConfigured += path =>
        {
            StatusBar.GamePath = path;
            AccountsPanel.CheckAssemblyStatus();
        };

        Guide.OnRequestNavigateToGuide += () =>
        {
            SelectedTabIndex = 4;
        };

        Guide.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GuideViewModel.HasAppUpdate) && Guide.HasAppUpdate && Guide.AvailableAppUpdate != null)
            {
                StatusBar.StatusMessage = $"[CẬP NHẬT] Đã có bản mới v{Guide.AvailableAppUpdate.Version}! Vào tab Hướng dẫn để xem chi tiết.";
            }
        };

        AccountsPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountsPanelViewModel.IsMasked))
            {
                AccountForm.IsMasked = AccountsPanel.IsMasked;
            }
            else if (e.PropertyName == nameof(AccountsPanelViewModel.SelectedAccountsCount))
            {
                UtilitiesTab.UpdateSyncTargetDisplay();
                AutoTab.UpdateSyncTargetDisplay();
            }
        };

        AccountForm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountFormViewModel.IsMasked))
            {
                if (AccountsPanel.IsMasked != AccountForm.IsMasked)
                {
                    AccountsPanel.IsMasked = AccountForm.IsMasked;
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

    private List<Guid> ResolveRunningTargetAccountIds()
    {
        var targets = AccountsPanel.GetTargetAccounts();
        var running = targets.Where(a => a.IsRunning).Select(a => a.Id).ToList();
        if (running.Count > 0) return running;

        if (AccountsPanel.SelectedAccount != null && AccountsPanel.SelectedAccount.IsRunning)
        {
            return [AccountsPanel.SelectedAccount.Id];
        }

        return [];
    }

    private List<Guid> ResolveRunningSpeedTargetAccountIds(bool applyToAll)
    {
        if (applyToAll)
        {
            var runningAll = AccountsPanel.Accounts.Where(a => a.IsRunning).Select(a => a.Id).ToList();
            if (runningAll.Count > 0) return runningAll;
        }

        return ResolveRunningTargetAccountIds();
    }

    public async Task InitializeAsync()
    {
        await AccountsPanel.LoadAccountsAsync();
        StatusBar.StatusMessage = $"Sẵn sàng ({AccountsPanel.TotalAccountsCount} tài khoản)";
    }
}
