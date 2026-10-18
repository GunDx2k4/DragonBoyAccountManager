using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Actions;

public abstract class GameAction(string name) : IGameAction
{
    public string Name { get; } = name;

    public async Task<ActionResult> ExecuteAsync(
        IGameContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested || !context.IsRunning)
        {
            return ActionResult.Failure("Hành động bị hủy hoặc game đã dừng.");
        }

        try
        {
            OnEnter(context);
            var result = await OnExecuteAsync(context, progress, cancellationToken);
            OnExit(context, result);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            progress?.Report($"[{Name}] Đã bị hủy bỏ.");
            return ActionResult.Failure("Hành động bị hủy.", ex);
        }
        catch (Exception ex)
        {
            progress?.Report($"[{Name}] Lỗi: {ex.Message}");
            return ActionResult.Failure(ex.Message, ex);
        }
    }

    protected virtual void OnEnter(IGameContext context) { }

    protected abstract Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    protected virtual void OnExit(IGameContext context, ActionResult result) { }
}
