
namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public class InterrupterPipeline
{
    private readonly List<IActionInterrupter> _interrupters = [];
    private readonly object _syncLock = new();

    public IReadOnlyList<IActionInterrupter> Interrupters
    {
        get
        {
            lock (_syncLock)
            {
                return [.. _interrupters];
            }
        }
    }

    public void Register(IActionInterrupter interrupter)
    {
        ArgumentNullException.ThrowIfNull(interrupter);
        lock (_syncLock)
        {
            _interrupters.Add(interrupter);
            _interrupters.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }

    public void Unregister(IActionInterrupter interrupter)
    {
        ArgumentNullException.ThrowIfNull(interrupter);
        lock (_syncLock)
        {
            _interrupters.Remove(interrupter);
        }
    }

    public async Task<ActionResult?> CheckAndExecuteAsync(IGameContext context, CancellationToken cancellationToken = default)
    {
        IActionInterrupter[] snapshot;
        lock (_syncLock)
        {
            snapshot = [.. _interrupters];
        }

        foreach (var interrupter in snapshot)
        {
            if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                return ActionResult.Failure("Bị hủy hoặc trò chơi đã dừng.");

            if (interrupter.ShouldInterrupt(context))
            {
                var result = await interrupter.HandleAsync(context, cancellationToken);
                return result;
            }
        }
        return null;
    }
}
