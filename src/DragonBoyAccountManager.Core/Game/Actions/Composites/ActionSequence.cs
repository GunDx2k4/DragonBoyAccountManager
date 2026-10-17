using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Actions.Composites;

/// <summary>
/// Thực hiện tuần tự danh sách các action.
/// Dừng lại ngay lập tức và trả về Failure nếu có bất kỳ action con nào thất bại.
/// </summary>
public class ActionSequence(string name, params IGameAction[] actions) : GameAction(name)
{
    private readonly List<IGameAction> _actions = [.. actions];

    public ActionSequence(params IGameAction[] actions) : this("Sequence", actions) { }

    public IReadOnlyList<IGameAction> Actions => _actions;

    public void Add(IGameAction action) => _actions.Add(action);

    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context, 
        IProgress<string>? progress, 
        CancellationToken cancellationToken)
    {
        for (int i = 0; i < _actions.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                return ActionResult.Failure("Sequence bị dừng giữa chừng.");

            var current = _actions[i];
            progress?.Report($"[{Name}] Bước {i + 1}/{_actions.Count}: {current.Name}");

            var result = await current.ExecuteAsync(context, progress, cancellationToken);
            if (!result.IsSuccess)
            {
                return ActionResult.Failure($"Bước '{current.Name}' thất bại: {result.Message}", result.Exception);
            }
        }

        return ActionResult.Success($"Hoàn thành toàn bộ chuỗi {Name}.");
    }
}
