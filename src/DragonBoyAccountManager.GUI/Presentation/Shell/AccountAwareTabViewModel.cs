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

public abstract partial class AccountAwareTabViewModel : AccountTargetedViewModelBase, IActivatableTab
{
    [ObservableProperty]
    private bool _hasActiveAccount;

    [ObservableProperty]
    private bool _isAccountRunning;

    [ObservableProperty]
    private string _activeAccountInfo = "Chưa chọn tài khoản";

    protected AccountAwareTabViewModel(
        IGameEngineService gameEngine,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, selectionService, dispatcher)
    {
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
}
