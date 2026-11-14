using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Presentation.Shell;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTabViewModel : AccountAwareTabViewModel
{
    private readonly IReadOnlyList<IAutoFeatureViewModel> _features;

    public AutoTrainViewModel Train { get; }
    public AutoChatViewModel Chat { get; }
    public AutoBossViewModel Boss { get; }

    public AutoTabViewModel(
        IGameEngineService gameEngine,
        AutoTrainViewModel trainViewModel,
        AutoChatViewModel chatViewModel,
        AutoBossViewModel bossViewModel,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
        : base(gameEngine, selectionService, dispatcher)
    {
        Train = trainViewModel;
        Chat = chatViewModel;
        Boss = bossViewModel;
        _features = [Train, Chat, Boss];

        _gameEngine.OnSessionEvent += HandleSessionEvent;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SyncTargetDisplay))
            {
                foreach (var feature in _features)
                {
                    feature.SyncTargetDisplay = SyncTargetDisplay;
                }
            }
        };
    }

    public override void OnTabActivated()
    {
        UpdateSyncTargetDisplay();
        foreach (var feature in _features)
        {
            feature.SyncTargetDisplay = SyncTargetDisplay;
            feature.OnActivated();
        }
    }

    public override void OnTabDeactivated()
    {
        SaveCurrentAccountSettings();
        foreach (var feature in _features)
        {
            feature.OnDeactivated();
        }
    }

    protected override void OnActiveSnapshotUpdated(GameSessionSnapshot snapshot)
    {
        foreach (var feature in _features)
        {
            feature.OnSnapshotUpdated(snapshot);
        }
    }

    protected override void OnActiveAccountOffline()
    {
        foreach (var feature in _features)
        {
            feature.OnAccountOffline();
        }
    }

    private void HandleSessionEvent(SessionEvent evt)
    {
        foreach (var feature in _features)
        {
            feature.OnSessionEvent(evt);
        }
    }

    public void SaveCurrentAccountSettings()
    {
        if (ActiveAccount == null) return;
        foreach (var feature in _features)
        {
            feature.SaveSettings(ActiveAccount);
        }
    }

    public override void SetActiveAccount(AccountItemViewModel? account)
    {
        if (ActiveAccount == account) return;

        if (ActiveAccount != null)
        {
            SaveCurrentAccountSettings();
        }

        ActiveAccount = account;
        HasActiveAccount = account != null;
        IsAccountRunning = account?.IsRunning == true;
        ActiveAccountInfo = account != null ? FormatAccountDisplayInfo(account) : "Chưa chọn tài khoản";

        foreach (var feature in _features)
        {
            feature.SetActiveAccount(account);
        }
    }
}
