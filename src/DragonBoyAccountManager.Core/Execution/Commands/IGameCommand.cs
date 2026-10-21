using DragonBoyAccountManager.Core.Common.Results;

namespace DragonBoyAccountManager.Core.Execution.Commands;

public interface IGameCommand
{
    string Name { get; }
}

public interface IGameCommandHandler<in TCommand> where TCommand : IGameCommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
