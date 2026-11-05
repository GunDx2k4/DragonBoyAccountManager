using System.Diagnostics;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface ISessionManager
{
    Action<GameSession>? SessionInitializer { get; set; }
    IReadOnlyCollection<Guid> ActiveSessionIds { get; }
    bool IsRunning(Guid accountId);
    int? GetProcessId(Guid accountId);
    Process? GetProcess(Guid accountId);
    GameSession? GetSession(Guid accountId);
    SessionWorker? GetWorker(Guid accountId);

    Task<bool> StartSessionAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default);
    Task StopSessionAsync(Guid accountId);
    Task StopAllSessionsAsync();
    void StopAllSessions();

    TSnapshot? GetSnapshot<TSnapshot>(Guid accountId) where TSnapshot : class;

    event Action<SessionEvent>? EventPublished;
}
