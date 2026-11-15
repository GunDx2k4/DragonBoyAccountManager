using CommunityToolkit.Mvvm.ComponentModel;
using DragonBoyAccountManager.GUI.Presentation.Features.Accounts.Items;
using DragonBoyAccountManager.GUI.Services.Dispatching;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Selection;

namespace DragonBoyAccountManager.GUI.Presentation.Shell;

public abstract partial class AccountTargetedViewModelBase : ObservableObject
{
    protected readonly IGameEngineService _gameEngine;
    protected readonly IActiveSelectionService? _selectionService;
    protected readonly IDispatcherService? _dispatcher;

    protected IGameEngineService GameEngine => _gameEngine;
    protected IActiveSelectionService? SelectionService => _selectionService;
    protected IDispatcherService? Dispatcher => _dispatcher;

    [ObservableProperty]
    private AccountItemViewModel? _activeAccount;

    [ObservableProperty]
    private string _syncTargetDisplay = "1 tài khoản";

    [ObservableProperty]
    private string _statusText = string.Empty;

    protected AccountTargetedViewModelBase(
        IGameEngineService gameEngine,
        IActiveSelectionService? selectionService = null,
        IDispatcherService? dispatcher = null)
    {
        _gameEngine = gameEngine;
        _selectionService = selectionService;
        _dispatcher = dispatcher;
    }

    protected void RunOnUi(Action action) => _dispatcher.Dispatch(action);

    public void UpdateSyncTargetDisplay()
    {
        int count = _selectionService?.TargetCount ?? (ActiveAccount != null ? 1 : 0);
        SyncTargetDisplay = count > 1
            ? $"Đồng bộ: {count} tài khoản"
            : (count == 1 ? "1 tài khoản" : "Chưa chọn tài khoản");
    }

    public AccountItemViewModel? ResolveAccount(Guid id) =>
        _selectionService?.FindAccount(id) ?? (ActiveAccount?.Id == id ? ActiveAccount : null);

    protected List<Guid> GetTargetAccountIds()
    {
        if (_selectionService != null)
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

    protected List<AccountItemViewModel> GetAllTargetAccounts()
    {
        if (_selectionService != null)
        {
            var targets = _selectionService.GetTargetAccounts();
            if (targets.Count > 0) return targets.ToList();
        }

        if (ActiveAccount != null)
        {
            return [ActiveAccount];
        }

        return [];
    }
}
