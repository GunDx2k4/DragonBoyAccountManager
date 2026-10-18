using DragonBoyAccountManager.Core.Game.Actions.Composites;
using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Actions.Fluent;

public class ActionBuilder
{
    private readonly List<IGameAction> _steps = [];
    private string _name = "CustomAction";

    public static ActionBuilder Create(string name = "CustomAction") => new() { _name = name };

    public ActionBuilder Do(IGameAction action)
    {
        _steps.Add(action);
        return this;
    }

    public ActionBuilder Do(string name, Func<IGameContext, CancellationToken, Task<bool>> action)
    {
        _steps.Add(new DelegateAction(name, action));
        return this;
    }

    public ActionBuilder Delay(int milliseconds)
    {
        _steps.Add(new DelayAction(milliseconds));
        return this;
    }

    public ActionBuilder WaitUntil(string conditionName, Func<IGameContext, bool> condition, TimeSpan? timeout = null)
    {
        _steps.Add(new WaitConditionAction(conditionName, condition, timeout));
        return this;
    }

    public ActionBuilder Repeat(int times, Action<ActionBuilder> configure)
    {
        var nested = new ActionBuilder();
        configure(nested);
        _steps.Add(new ActionRepeat(nested.Build(), times));
        return this;
    }

    public IGameAction Build()
    {
        if (_steps.Count == 1) return _steps[0];
        return new ActionSequence(_name, [.. _steps]);
    }
}

public class DelayAction(int milliseconds) : GameAction($"Delay({milliseconds}ms)")
{
    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await Task.Delay(milliseconds, cancellationToken);
        return ActionResult.Success();
    }
}

public class WaitConditionAction(string conditionName, Func<IGameContext, bool> condition, TimeSpan? timeout = null)
    : GameAction($"WaitUntil({conditionName})")
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromSeconds(10);

    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested && context.IsRunning)
        {
            if (condition(context))
                return ActionResult.Success();

            if (sw.Elapsed > _timeout)
                return ActionResult.Failure($"Hết thời gian chờ điều kiện '{conditionName}'.");

            await Task.Delay(100, cancellationToken);
        }
        return ActionResult.Failure("Bị hủy trong khi chờ.");
    }
}

public class DelegateAction(string name, Func<IGameContext, CancellationToken, Task<bool>> func)
    : GameAction(name)
{
    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        bool ok = await func(context, cancellationToken);
        return ok ? ActionResult.Success() : ActionResult.Failure($"Action '{Name}' thực hiện không thành công.");
    }
}
