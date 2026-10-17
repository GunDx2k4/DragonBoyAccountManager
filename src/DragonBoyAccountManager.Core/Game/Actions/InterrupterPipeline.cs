using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Actions;

/// <summary>
/// Định nghĩa một Interrupter phản xạ tình huống khẩn cấp (như bị quái đánh chết, dialog bất ngờ xuất hiện, dis mạng).
/// </summary>
public interface IActionInterrupter
{
    string Name { get; }
    int Priority { get; }
    bool ShouldInterrupt(IGameContext context);
    Task<ActionResult> HandleAsync(IGameContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pipeline quản lý và kích hoạt các phản xạ ưu tiên cao trước khi Action chính được chạy.
/// </summary>
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
