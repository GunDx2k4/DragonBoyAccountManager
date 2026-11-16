using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Execution.Handlers;

namespace DragonBoyAccountManager.Core.Tests.Mocks;

public sealed class MockCommandDispatcher : ICommandDispatcher
{
    private readonly List<IGameCommand> _commands = [];
    public IReadOnlyList<IGameCommand> DispatchedCommands => _commands;

    public event Action<IGameCommand, Result>? CommandExecuted;
    public event Action<IGameCommand>? CommandExecuting;

    public void RegisterHandler<TCommand>(IGameCommandHandler<TCommand> handler) where TCommand : IGameCommand
    {
    }

    public Task<Result> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : IGameCommand
    {
        _commands.Add(command);
        CommandExecuting?.Invoke(command);
        var result = Result.Success();
        CommandExecuted?.Invoke(command, result);
        return Task.FromResult(result);
    }

    public void Clear() => _commands.Clear();
}
