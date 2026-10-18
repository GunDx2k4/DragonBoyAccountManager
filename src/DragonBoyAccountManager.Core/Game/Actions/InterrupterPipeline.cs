using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Actions;

public interface IActionInterrupter
{
    string Name { get; }
    int Priority { get; }
    bool ShouldInterrupt(IGameContext context);
    Task<ActionResult> HandleAsync(IGameContext context, CancellationToken cancellationToken = default);
}

public class InterrupterPipeline
{
    private readonly List<IActionInterrupter> _interrupters = [];

    public IReadOnlyList<IActionInterrupter> Interrupters => _interrupters;

    public void Register(IActionInterrupter interrupter)
    {
        _interrupters.Add(interrupter);
        _interrupters.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    public void Unregister(IActionInterrupter interrupter)
    {
        _interrupters.Remove(interrupter);
    }

    public async Task<ActionResult?> CheckAndExecuteAsync(IGameContext context, CancellationToken cancellationToken = default)
    {
        foreach (var interrupter in _interrupters)
        {
            if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                return ActionResult.Failure("Bị hủy hoặc game đã dừng.");

            if (interrupter.ShouldInterrupt(context))
            {
                var result = await interrupter.HandleAsync(context, cancellationToken);
                return result;
            }
        }
        return null;
    }
}
