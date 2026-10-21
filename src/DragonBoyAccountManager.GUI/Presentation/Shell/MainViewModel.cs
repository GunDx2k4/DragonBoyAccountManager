using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Common;

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
                StatusBar.StatusMessage = $"[CẬP NHẬT] Đã có bản mới v{Guide.AvailableAppUpdate.Version}! Vào tab Hướng dẫn để xem chi tiết.";
            }
        };

        AccountsPanel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountsPanelViewModel.IsMasked))
            {
                if (AccountForm.IsMasked != AccountsPanel.IsMasked)
                {
                    AccountForm.IsMasked = AccountsPanel.IsMasked;
                }
            }
        };

        AccountForm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AccountFormViewModel.IsMasked))
            {
                if (AccountsPanel.IsMasked != AccountForm.IsMasked)
                {
                    AccountsPanel.IsMasked = AccountForm.IsMasked;
                }
            }
        };

        AccountForm.IsMasked = AccountsPanel.IsMasked;

        AccountForm.OnAccountSaved += async account =>
        {
            try
            {
                bool isNew = !AccountsPanel.Accounts.Any(a => a.Id == account.Id);
                await _storageService.AddOrUpdateAsync(account);
                AccountsPanel.AddOrUpdateItem(account);
                string displayUser = AccountsPanel.IsMasked ? TextMaskHelper.MaskText(account.Username) : account.Username;

                if (isNew)
                {
                    StatusBar.StatusMessage = $"Đã thêm tài khoản mới: {displayUser}";
                    _dialogService.ShowInfo("Thành công", $"Đã thêm tài khoản mới '{displayUser}' thành công!");
                }
                else
                {
                    StatusBar.StatusMessage = $"Đã cập nhật thông tin tài khoản: {displayUser}";
                    _dialogService.ShowInfo("Thành công", $"Đã cập nhật thông tin tài khoản '{displayUser}' thành công!");
                }
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
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Lỗi khởi động: {ex.Message}");
        }
    }

    public async Task InitializeAsync()
    {
        await AccountsPanel.LoadAccountsAsync();
        if (AccountsPanel.Accounts.Count > 0 && AccountsPanel.SelectedAccount == null)
        {
            AccountsPanel.SelectedAccount = AccountsPanel.Accounts[0];
        }
        StatusBar.StatusMessage = $"Sẵn sàng ({AccountsPanel.TotalAccountsCount} tài khoản)";
    }
}
