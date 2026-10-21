using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public class AutoReviveInterrupter : IInterrupter
{
    public string Name => "AutoRevive";

    public async Task<Result?> CheckAndExecuteAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (player.IsInGame && !player.IsAlive)
        {
            await dispatcher.DispatchAsync(new WakeUpCommand(), cancellationToken);
            await Task.Delay(1500, cancellationToken);
            return Result.Success("Nhân vật tử vong, đã gửi lệnh hồi sinh về nhà.");
        }
        return null;
    }
}
