using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;

namespace DragonBoyAccountManager.Core.Execution;

public interface ICommandDispatcher
{
    void AddMiddleware(ICommandMiddleware middleware);
    void RegisterHandler<TCommand>(IGameCommandHandler<TCommand> handler) where TCommand : IGameCommand;

    Task<Result> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : IGameCommand;

    event Action<IGameCommand, Result>? CommandExecuted;
    event Action<IGameCommand>? CommandExecuting;
}
