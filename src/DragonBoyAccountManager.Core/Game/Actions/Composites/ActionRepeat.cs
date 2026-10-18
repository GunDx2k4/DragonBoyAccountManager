using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Actions.Composites;

public class ActionRepeat(IGameAction action, int repeatCount = -1, TimeSpan? delayBetween = null)
    : GameAction($"Repeat({action?.Name})")
{
    private readonly IGameAction _action = action ?? throw new ArgumentNullException(nameof(action));
    private readonly int _repeatCount = repeatCount;
    private readonly TimeSpan _delayBetween = delayBetween ?? TimeSpan.FromMilliseconds(100);

    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        int count = 0;
        while (!cancellationToken.IsCancellationRequested && context.IsRunning)
        {
            if (_repeatCount > 0 && count >= _repeatCount)
                break;

            count++;
            var result = await _action.ExecuteAsync(context, progress, cancellationToken);
            if (!result.IsSuccess)
            {
                return ActionResult.Failure($"Lặp bị dừng ở lần {count} do action con thất bại: {result.Message}", result.Exception);
            }

            if (_delayBetween > TimeSpan.Zero)
            {
                await Task.Delay(_delayBetween, cancellationToken);
            }
        }

        return ActionResult.Success($"Hoàn thành {count} lần lặp.");
    }
}
