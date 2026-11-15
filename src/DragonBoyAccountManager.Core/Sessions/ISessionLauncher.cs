using DragonBoyAccountManager.Core.Engine.Models;

namespace DragonBoyAccountManager.Core.Sessions;

public interface ISessionLauncher
{
    Task<GameSession> LaunchAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default);
}
