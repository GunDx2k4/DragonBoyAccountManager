using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Domain.Messaging;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Shell;

public abstract partial class AccountAwareTabViewModel : ObservableObject, IActivatableTab
{
    protected readonly IGameEngineService _gameEngine;
    protected readonly IActiveSelectionService? _selectionService;
    protected readonly IDispatcherService? _dispatcher;

    [ObservableProperty]
    private AccountItemViewModel? _activeAccount;

    [ObservableProperty]
    private bool _hasActiveAccount;

    [ObservableProperty]
    private bool _isAccountRunning;

    [ObservableProperty]
    private string _activeAccountInfo = "Chưa chọn tài khoản";

    [ObservableProperty]
    private string _syncTargetDisplay = "1 tài khoản";

    [ObservableProperty]
    private string _statusText = string.Empty;

    protected AccountAwareTabViewModel(
        IGameEngineService gameEngine,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
    {
        _gameEngine = gameEngine;
        _selectionService = selectionService;
        _dispatcher = dispatcher;

        if (_selectionService != null)
        {
            _selectionService.SelectedAccountChanged += account =>
            {
                SetActiveAccount(account);
                UpdateSyncTargetDisplay();
            };
            _selectionService.TargetsChanged += UpdateSyncTargetDisplay;
        }

        WeakReferenceMessenger.Default.Register<SessionSnapshotUpdatedMessage>(this, (_, m) =>
        {
            if (ActiveAccount?.Id == m.AccountId)
            {
                RunOnUi(() =>
                {
                    if (m.Status == GameSessionStatus.Active && m.Snapshot != null)
                    {
                        IsAccountRunning = true;
                        OnActiveSnapshotUpdated(m.Snapshot);
                    }
                    else if (m.Status == GameSessionStatus.Offline)
                    {
                        IsAccountRunning = false;
                        OnActiveAccountOffline();
                    }
                });
            }
        });
    }

    protected void RunOnUi(Action action)
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

    protected static string FormatAccountDisplayInfo(AccountItemViewModel account)
    {
        string server = !string.IsNullOrWhiteSpace(account.Server) ? $" ({account.Server})" : string.Empty;
        string charName = !string.IsNullOrWhiteSpace(account.DisplayCharacterName) ? $" [{account.DisplayCharacterName}]" : string.Empty;
        return $"{account.DisplayUsername}{server}{charName}";
    }

    public abstract void SetActiveAccount(AccountItemViewModel? account);

    protected abstract void OnActiveSnapshotUpdated(GameSessionSnapshot snapshot);

    protected abstract void OnActiveAccountOffline();

    public virtual void OnTabActivated() { }

    public virtual void OnTabDeactivated() { }

    public void UpdateSyncTargetDisplay()
    {
        var targets = GetTargetAccountIds(true);
        int count = targets.Count;
        SyncTargetDisplay = count > 1
            ? $"Đồng bộ: {count} tài khoản"
            : (count == 1 ? "1 tài khoản" : "Chưa chọn tài khoản");
    }

    public AccountItemViewModel? ResolveAccount(Guid id) =>
        _selectionService?.FindAccount(id) ?? (ActiveAccount?.Id == id ? ActiveAccount : null);

    protected List<Guid> GetTargetAccountIds(bool syncAll = true)
    {
        if (syncAll && _selectionService != null)
        {
            var running = _selectionService.GetRunningTargetAccountIds();
            if (running.Count > 0) return running.ToList();
        }

        if (ActiveAccount != null && ActiveAccount.IsRunning)
        {
            return [ActiveAccount.Id];
        }

        return [];
    }
}
