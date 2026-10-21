using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAccountStorageService _storageService;
    private readonly IDialogService _dialogService;
    private readonly IGameInputService _gameInput;

    public AccountsPanelViewModel AccountsPanel { get; }
    public AccountFormViewModel AccountForm { get; }
    public AppStatusBarViewModel StatusBar { get; }
    public SettingsTabViewModel Settings { get; }
    public GuideViewModel Guide { get; }
    public UtilitiesTabViewModel UtilitiesTab { get; }
    public AutoTabViewModel AutoTab { get; }
    public FeedbackViewModel Feedback { get; }
    public AppTitleBarViewModel TitleBar { get; }

    [ObservableProperty]
    private int _selectedTabIndex;

    public MainViewModel(
        AccountsPanelViewModel accountsPanel,
        AccountFormViewModel accountForm,
        AppStatusBarViewModel statusBar,
        AppTitleBarViewModel titleBar,
        SettingsTabViewModel settings,
        GuideViewModel guide,
        UtilitiesTabViewModel utilitiesTab,
        AutoTabViewModel autoTab,
        FeedbackViewModel feedback,
        IAccountStorageService storageService,
        IDialogService dialogService,
        IGameInputService gameInput)
    {
        AccountsPanel = accountsPanel;
        AccountForm = accountForm;
        StatusBar = statusBar;
        TitleBar = titleBar;
        Settings = settings;
        Guide = guide;
        UtilitiesTab = utilitiesTab;
        AutoTab = autoTab;
        Feedback = feedback;
        _storageService = storageService;
        _dialogService = dialogService;
        _gameInput = gameInput;

        WeakReferenceMessenger.Default.Register<RequestNavigateToTabMessage>(this, (_, m) => SelectedTabIndex = m.TabIndex);

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedTabIndex))
            {
                if (SelectedTabIndex == 0)
                {
                    UtilitiesTab.StopPolling();
                    AutoTab.SaveCurrentAccountSettings();
                    AutoTab.StopPolling();
                    AccountForm.StartPolling();
                    AccountForm.RefreshSkills();
                    AccountForm.RequestZoneList();
                }
                else if (SelectedTabIndex == 1)
                {
                    AccountForm.StopPolling();
                    AutoTab.SaveCurrentAccountSettings();
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
                    AutoTab.SaveCurrentAccountSettings();
                    AutoTab.StopPolling();
                }
            }
        };

        if (SelectedTabIndex == 0)
        {
            AccountForm.StartPolling();
        }

        AccountsPanel.OnStatusMessageUpdated += msg =>
        {
            StatusBar.StatusMessage = msg;
        };

        AccountsPanel.OnRequestNavigateToGuide += () =>
        {
            SelectedTabIndex = 4;
        };

        Guide.OnRequestNavigateToGuide += () =>
        {
            SelectedTabIndex = 4;
        };

        Guide.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GuideViewModel.HasAppUpdate) && Guide.HasAppUpdate && Guide.AvailableAppUpdate != null)
            {
                StatusBar.StatusMessage = $"[C?P NH?T] Ðã có b?n m?i v{Guide.AvailableAppUpdate.Version}! Vào tab Hu?ng d?n d? xem chi ti?t.";
            }
        };

        AccountsPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountsPanelViewModel.IsMasked))
            {
                AccountForm.IsMasked = AccountsPanel.IsMasked;
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
                StatusBar.StatusMessage = $"Ðã luu tài kho?n: {account.Username}";
                _dialogService.ShowInfo("Thành công", $"Ðã luu tài kho?n '{account.Username}' thành công!");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("L?i", $"Không th? luu tài kho?n: {ex.Message}");
            }
        };

        AccountForm.OnAccountDeleted += async id =>
        {
            try
            {
                await _storageService.DeleteAsync(id);
                AccountsPanel.RemoveItem(id);
                StatusBar.StatusMessage = "Ðã xóa tài kho?n.";
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("L?i", $"Không th? xóa tài kho?n: {ex.Message}");
            }
        };

        _ = InitializeSafeAsync();
    }

    private async Task InitializeSafeAsync()
    {
        try
        {
            await InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] L?i kh?i d?ng: {ex.Message}");
        }
    }

    public async Task InitializeAsync()
    {
        await AccountsPanel.LoadAccountsAsync();
        if (AccountsPanel.Accounts.Count > 0 && AccountsPanel.SelectedAccount == null)
        {
            AccountsPanel.SelectedAccount = AccountsPanel.Accounts[0];
        }
        StatusBar.StatusMessage = $"S?n sàng ({AccountsPanel.TotalAccountsCount} tài kho?n)";
    }
}
