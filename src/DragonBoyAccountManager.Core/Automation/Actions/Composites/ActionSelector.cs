
namespace DragonBoyAccountManager.Core.Automation.Actions.Composites;

public class ActionSelector(string name, params IGameAction[] actions) : GameAction(name)
{
    private readonly List<IGameAction> _actions = [.. actions];

    public ActionSelector(params IGameAction[] actions) : this("Selector", actions) { }

    public IReadOnlyList<IGameAction> Actions => _actions;

    public void Add(IGameAction action) => _actions.Add(action);

    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        foreach (var action in _actions)
        {
            if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                return ActionResult.Failure("Selector bị dừng.");

            var result = await action.ExecuteAsync(context, progress, cancellationToken);
            if (result.IsSuccess)
            {
                return result;
            }
        }

        return ActionResult.Failure($"Tất cả các nhánh trong '{Name}' đều không thành công.");
    }
}
