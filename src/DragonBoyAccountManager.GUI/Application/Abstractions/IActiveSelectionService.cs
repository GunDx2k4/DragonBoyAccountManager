using System;
using System.Collections.Generic;

namespace DragonBoyAccountManager.GUI.Application.Abstractions;

public interface IActiveSelectionService
{
    AccountItemViewModel? SelectedAccount { get; set; }
    int TargetCount { get; }
    string SyncTargetDisplayText { get; }

    void RegisterAccountsProvider(
        Func<IReadOnlyList<AccountItemViewModel>> allAccountsProvider,
        Func<IReadOnlyList<AccountItemViewModel>> targetAccountsProvider);

    IReadOnlyList<AccountItemViewModel> GetTargetAccounts();
    IReadOnlyList<Guid> GetRunningTargetAccountIds();
    IReadOnlyList<Guid> GetRunningSpeedTargetAccountIds(bool applyToAll);
    AccountItemViewModel? FindAccount(Guid id);

    void NotifyTargetsChanged();

    event Action<AccountItemViewModel?>? SelectedAccountChanged;
    event Action? TargetsChanged;
}
