using System.Diagnostics;
using DragonBoyAccountManager.Core.Engine.Models;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface IGameEngine :
    IGameActionService,
    IGameAutomationService,
    IGameEngineEvents,
    IDisposable
{
    ISessionManager Sessions { get; }

    Task<bool> StartAccountAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default);
    Task StopAccountAsync(Guid accountId);
    Task StopAllAccountsAsync();
    void StopAllAccounts();

    int? GetProcessId(Guid accountId);
    Process? GetProcess(Guid accountId);

    Task<bool> LoginAsync(Guid accountId);

    void SetMasked(Guid accountId, bool isMasked);
    void SetAllMasked(bool isMasked);
}
