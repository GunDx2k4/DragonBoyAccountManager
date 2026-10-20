
namespace DragonBoyAccountManager.Core.Automation.Actions.Primitives;

public class WakeUpAction() : GameAction("Hồi sinh nhân vật")
{
    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!context.IsRunning || !context.Player.IsInGame)
        {
            return ActionResult.Failure("Nhân vật chưa vào game.");
        }

        if (context.Player.IsAlive)
        {
            return ActionResult.Success("Nhân vật vẫn còn sống.");
        }

        progress?.Report("[WakeUp] Đang gửi yêu cầu hồi sinh về máy chủ...");
        bool sent = context.Network.WakeUpFromDead();
        if (!sent)
        {
            return ActionResult.Failure("Không thể gửi yêu cầu hồi sinh.");
        }

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested && context.IsRunning)
        {
            await Task.Delay(200, cancellationToken);
            if (context.Player.IsAlive)
            {
                progress?.Report("[WakeUp] Nhân vật đã hồi sinh thành công.");
                return ActionResult.Success("Nhân vật đã hồi sinh thành công.");
            }
        }

        return ActionResult.Success("Đã gửi yêu cầu hồi sinh (đang chờ máy chủ xử lý).");
    }
}
