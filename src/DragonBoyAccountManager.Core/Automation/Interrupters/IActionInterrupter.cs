
namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public interface IActionInterrupter
{
    string Name { get; }
    int Priority { get; }
    bool ShouldInterrupt(IGameContext context);
    Task<ActionResult> HandleAsync(IGameContext context, CancellationToken cancellationToken = default);
}
