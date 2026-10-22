using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Common;

namespace DragonBoyAccountManager.GUI.Presentation.Shell;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAccountStorageService _storageService;
    private readonly IDialogService _dialogService;
    private readonly IGameInputService _gameInput;
    private readonly IReadOnlyDictionary<AppTab, IActivatableTab> _tabs;

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

    partial void OnSelectedTabIndexChanged(int oldValue, int newValue)
    {
        if (_tabs.TryGetValue((AppTab)oldValue, out var oldTab))
        {
            oldTab.OnTabDeactivated();
        }
        if (_tabs.TryGetValue((AppTab)newValue, out var newTab))
        {
            newTab.OnTabActivated();
        }
    }

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

        _tabs = new Dictionary<AppTab, IActivatableTab>
        {
            [AppTab.AccountConfig] = AccountForm,
            [AppTab.MapUtilities] = UtilitiesTab,
            [AppTab.Auto] = AutoTab
        };

        WeakReferenceMessenger.Default.Register<RequestNavigateToTabMessage>(this, (_, m) => SelectedTabIndex = (int)m.Tab);

        if (_tabs.TryGetValue((AppTab)SelectedTabIndex, out var initialTab))
        {
            initialTab.OnTabActivated();
        }

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
                    WeakReferenceMessenger.Default.Send(new StatusMessageChangedMessage($"Đã thêm tài khoản mới: {displayUser}"));
                    _dialogService.ShowInfo("Thành công", $"Đã thêm tài khoản mới '{displayUser}' thành công!");
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new StatusMessageChangedMessage($"Đã cập nhật thông tin tài khoản: {displayUser}"));
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
                WeakReferenceMessenger.Default.Send(new StatusMessageChangedMessage("Đã xóa tài khoản."));
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
        WeakReferenceMessenger.Default.Send(new StatusMessageChangedMessage($"Sẵn sàng ({AccountsPanel.TotalAccountsCount} tài khoản)"));
    }
}
