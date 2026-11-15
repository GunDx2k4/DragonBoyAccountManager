using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface IGameActionService
{
    bool IsRunning(Guid accountId);
    Task<Result> ExecuteCommandAsync(Guid accountId, IGameCommand command, CancellationToken cancellationToken = default);
    TSnapshot? GetSnapshot<TSnapshot>(Guid accountId) where TSnapshot : class;

    void SendGameKeyDown(Guid accountId, int keyCode);
    void SendGameKeyUp(Guid accountId, int keyCode);
}
