using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public interface IInterrupter
{
    string Name { get; }

    Task<Result?> CheckAndExecuteAsync(
        ISessionContext? context,
        PlayerSnapshot player,
        WorldSnapshot world,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
        => CheckAndExecuteAsync(player, world, dispatcher, cancellationToken);

    Task<Result?> CheckAndExecuteAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken);
}
