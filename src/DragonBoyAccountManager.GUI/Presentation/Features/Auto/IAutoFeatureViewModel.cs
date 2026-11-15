using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;

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
