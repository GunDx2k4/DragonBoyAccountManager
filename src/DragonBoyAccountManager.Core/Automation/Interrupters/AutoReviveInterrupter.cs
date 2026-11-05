using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public class AutoReviveInterrupter : IInterrupter
{
    public string Name => "AutoRevive";

    public async Task<Result?> CheckAndExecuteAsync(
        ISessionContext context,
        CancellationToken cancellationToken = default)
    {
        var player = context.CurrentPlayer;
        if (player.IsInGame && !player.IsAlive)
        {
            await context.Dispatcher.DispatchAsync(new WakeUpCommand(), cancellationToken);
            await Task.Delay(1500, cancellationToken);
            return Result.Success("Nhân vật tử vong, đã gửi lệnh hồi sinh về nhà.");
        }
        return null;
    }
}
