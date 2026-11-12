using CommunityToolkit.Mvvm.ComponentModel;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public interface IAutoFeatureViewModel
{
    string BehaviorId { get; }
    bool IsRunning { get; }
    string StatusText { get; }
    string SyncTargetDisplay { get; set; }

    void SetActiveAccount(AccountItemViewModel? account);
    void SaveSettings(AccountItemViewModel account);
    void OnActivated();
    void OnDeactivated();
    void OnSnapshotUpdated(GameSessionSnapshot snapshot);
    void OnAccountOffline();
    void OnSessionEvent(SessionEvent evt);
}

public abstract partial class AutoFeatureViewModelBase : ObservableObject, IAutoFeatureViewModel
{
    protected readonly IGameEngineService GameEngine;
    protected readonly IAccountStorageService StorageService;
    protected readonly IActiveSelectionService? SelectionService;
    protected readonly IDispatcherService? Dispatcher;

    protected AccountItemViewModel? ActiveAccount { get; private set; }
    protected bool IsSwitchingAccount { get; private set; }

    public abstract string BehaviorId { get; }

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Chưa kích hoạt";

    [ObservableProperty]
    private string _syncTargetDisplay = "1 tài khoản";

    protected AutoFeatureViewModelBase(
        IGameEngineService gameEngine,
        IAccountStorageService storageService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
    {
        GameEngine = gameEngine;
        StorageService = storageService;
        SelectionService = selectionService;
        Dispatcher = dispatcher;
    }

    public void SetActiveAccount(AccountItemViewModel? account)
    {
        ActiveAccount = account;
        if (account == null)
        {
            OnAccountOffline();
            return;
        }

        IsSwitchingAccount = true;
        try
        {
            LoadAccountCore(account);
        }
        finally
        {
            IsSwitchingAccount = false;
        }
    }

    public void SaveSettings(AccountItemViewModel account)
    {
        if (IsSwitchingAccount) return;
        SaveAccountCore(account);
        _ = StorageService.AddOrUpdateAsync(account.Model);
    }

    protected void PersistActiveAccountSettings()
    {
        if (IsSwitchingAccount || ActiveAccount == null) return;
        SaveSettings(ActiveAccount);
    }

    protected abstract void LoadAccountCore(AccountItemViewModel account);
    protected abstract void SaveAccountCore(AccountItemViewModel account);

    public virtual void OnActivated() { }
    public virtual void OnDeactivated() { }
    public virtual void OnSnapshotUpdated(GameSessionSnapshot snapshot) { }

    public virtual void OnAccountOffline()
    {
        IsRunning = false;
        StatusText = "Chưa kích hoạt";
    }

    public virtual void OnSessionEvent(SessionEvent evt)
    {
        switch (evt)
        {
            case BehaviorProgressEvent progress when string.Equals(progress.BehaviorId, BehaviorId, StringComparison.OrdinalIgnoreCase):
                OnBehaviorProgress(progress.AccountId, progress.Message);
                break;
            case BehaviorStateChangedEvent state when string.Equals(state.BehaviorId, BehaviorId, StringComparison.OrdinalIgnoreCase):
                OnBehaviorStateChanged(state.AccountId, state.IsRunning);
                break;
        }
    }

    protected virtual void OnBehaviorProgress(Guid accountId, string message)
    {
        RunOnUi(() =>
        {
            var target = ResolveAccount(accountId);
            if (target != null)
            {
                target.GetBehaviorState(BehaviorId).StatusText = message;
            }

            if (ActiveAccount?.Id == accountId)
            {
                StatusText = message;
            }
        });
    }

    protected virtual void OnBehaviorStateChanged(Guid accountId, bool isRunning)
    {
        RunOnUi(() =>
        {
            var target = ResolveAccount(accountId);
            if (target != null)
            {
                target.GetBehaviorState(BehaviorId).IsRunning = isRunning;
            }

            if (ActiveAccount?.Id == accountId)
            {
                IsRunning = isRunning;
            }
        });
    }

    protected AccountItemViewModel? ResolveAccount(Guid id) =>
        SelectionService?.FindAccount(id) ?? (ActiveAccount?.Id == id ? ActiveAccount : null);

    protected List<Guid> GetTargetAccountIds()
    {
        if (SelectionService != null)
        {
            var running = SelectionService.GetRunningTargetAccountIds();
            if (running.Count > 0) return running.ToList();
        }

        if (ActiveAccount != null && ActiveAccount.IsRunning)
        {
            return [ActiveAccount.Id];
        }

        return [];
    }

    protected List<AccountItemViewModel> GetAllTargetAccounts()
    {
        if (SelectionService != null)
        {
            var targets = SelectionService.GetTargetAccounts();
            if (targets.Count > 0) return targets.ToList();
        }

        if (ActiveAccount != null)
        {
            return [ActiveAccount];
        }

        return [];
    }

    protected void RunOnUi(Action action) => Dispatcher.Dispatch(action);
}
