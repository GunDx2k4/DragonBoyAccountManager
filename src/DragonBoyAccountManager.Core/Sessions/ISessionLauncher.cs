namespace DragonBoyAccountManager.Core.Sessions;

public interface ISessionLauncher
{
    Task<GameSession> LaunchAsync(LaunchOptions options, CancellationToken cancellationToken = default);
}
