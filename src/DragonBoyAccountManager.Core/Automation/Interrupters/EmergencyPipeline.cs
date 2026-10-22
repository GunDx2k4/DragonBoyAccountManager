using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public class EmergencyPipeline
{
    private readonly List<IInterrupter> _interrupters = [];

    public void Register(IInterrupter interrupter)
    {
        _interrupters.Add(interrupter);
    }

    public Task<Result?> CheckAndExecuteAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken = default)
        => CheckAndExecuteAsync(null, player, world, dispatcher, cancellationToken);

    public async Task<Result?> CheckAndExecuteAsync(
        ISessionContext? context,
        PlayerSnapshot player,
        WorldSnapshot world,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken = default)
    {
        foreach (var interrupter in _interrupters)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var result = await interrupter.CheckAndExecuteAsync(context, player, world, dispatcher, cancellationToken);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }
}
