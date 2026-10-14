namespace DragonBoyAccountManager.Core.Game;

public interface IGameAction
{
    string Name { get; }

    Task<bool> ExecuteAsync(GameInstance instance, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
