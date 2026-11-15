using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Presentation.Shell;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public abstract partial class AutoFeatureViewModelBase : AccountTargetedViewModelBase, IAutoFeatureViewModel
{
    protected readonly IAccountStorageService StorageService;

    protected bool IsSwitchingAccount { get; private set; }
    protected bool IsBulkUpdating { get; private set; }

    public abstract string BehaviorId { get; }

    [ObservableProperty]
    private bool _isRunning;

    protected AutoFeatureViewModelBase(
        IGameEngineService gameEngine,
        IAccountStorageService storageService,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, selectionService, dispatcher)
    {
        StorageService = storageService;
        StatusText = "Chưa kích hoạt";
    }

    protected void RunBulkUpdate(Action action)
    {
        IsBulkUpdating = true;
        try
        {
            action();
        }
        finally
        {
            IsBulkUpdating = false;
        }
    }

    protected virtual bool IsOptionProperty(string propertyName) =>
        propertyName is not (nameof(ActiveAccount) or nameof(SyncTargetDisplay) or nameof(StatusText) or nameof(IsRunning));

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (!IsSwitchingAccount
            && !IsBulkUpdating
            && ActiveAccount != null
            && !string.IsNullOrEmpty(e.PropertyName)
            && IsOptionProperty(e.PropertyName))
        {
            OnOptionPropertyChanged(e.PropertyName);
            OnOptionChanged();
        }
    }

    protected virtual void OnOptionPropertyChanged(string propertyName) { }
    protected virtual void OnOptionChanged() { }

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
}
