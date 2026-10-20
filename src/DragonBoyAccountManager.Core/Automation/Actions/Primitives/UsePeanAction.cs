
namespace DragonBoyAccountManager.Core.Automation.Actions.Primitives;

public class UsePeanAction() : GameAction("Sử dụng đậu thần")
{
    protected override Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!context.IsRunning || !context.Player.IsInGame)
        {
            return Task.FromResult(ActionResult.Failure("Nhân vật chưa vào game."));
        }

        var result = context.Player.UsePean();
        if (result.Success)
        {
            progress?.Report("[UsePean] Đã sử dụng đậu thần thành công.");
            return Task.FromResult(ActionResult.Success("Đã sử dụng đậu thần."));
        }

        return Task.FromResult(ActionResult.Failure(result.Message));
    }
}
