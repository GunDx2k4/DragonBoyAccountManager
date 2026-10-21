using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;

namespace DragonBoyAccountManager.Core.Execution;

public delegate Task<Result> CommandExecutionDelegate(IGameCommand command, CancellationToken cancellationToken);

public interface ICommandMiddleware
{
    Task<Result> InvokeAsync(IGameCommand command, CommandExecutionDelegate next, CancellationToken cancellationToken);
}
