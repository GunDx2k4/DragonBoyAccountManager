using System;
using System.Collections.Generic;
using System.Linq;

namespace DragonBoyAccountManager.GUI.Services.Selection;

public sealed class ActiveSelectionStore : IActiveSelectionService
{
    private AccountItemViewModel? _selectedAccount;
    private Func<IReadOnlyList<AccountItemViewModel>>? _allAccountsProvider;
    private Func<IReadOnlyList<AccountItemViewModel>>? _targetAccountsProvider;

    public AccountItemViewModel? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (!ReferenceEquals(_selectedAccount, value))
            {
                _selectedAccount = value;
                SelectedAccountChanged?.Invoke(value);
                TargetsChanged?.Invoke();
            }
        }
    }

    public int TargetCount => GetTargetAccounts().Count;

    public string SyncTargetDisplayText
    {
        get
        {
            int count = TargetCount;
            if (count > 1) return $"{count} tài khoản";
            return _selectedAccount != null ? "1 tài khoản" : "Chưa chọn tài khoản";
        }
    }

    public event Action<AccountItemViewModel?>? SelectedAccountChanged;
    public event Action? TargetsChanged;

    public void RegisterAccountsProvider(
        Func<IReadOnlyList<AccountItemViewModel>> allAccountsProvider,
        Func<IReadOnlyList<AccountItemViewModel>> targetAccountsProvider)
    {
        _allAccountsProvider = allAccountsProvider;
        _targetAccountsProvider = targetAccountsProvider;
    }

    public IReadOnlyList<AccountItemViewModel> GetTargetAccounts()
    {
        var targets = _targetAccountsProvider?.Invoke() ?? [];
        if (targets.Count > 0) return targets;
        if (_selectedAccount != null) return [_selectedAccount];
        return [];
    }

    public IReadOnlyList<Guid> GetRunningTargetAccountIds()
    {
        var targets = GetTargetAccounts();
        var running = targets.Where(a => a.IsRunning).Select(a => a.Id).ToList();
        if (running.Count > 0) return running;

        if (_selectedAccount != null && _selectedAccount.IsRunning)
        {
            return [_selectedAccount.Id];
        }

        return [];
    }

    public IReadOnlyList<Guid> GetRunningSpeedTargetAccountIds(bool applyToAll)
    {
        if (applyToAll && _allAccountsProvider != null)
        {
            var runningAll = _allAccountsProvider().Where(a => a.IsRunning).Select(a => a.Id).ToList();
            if (runningAll.Count > 0) return runningAll;
        }

        return GetRunningTargetAccountIds();
    }

    public AccountItemViewModel? FindAccount(Guid id)
    {
        return _allAccountsProvider?.Invoke().FirstOrDefault(a => a.Id == id);
    }

    public void NotifyTargetsChanged()
    {
        TargetsChanged?.Invoke();
    }
}
