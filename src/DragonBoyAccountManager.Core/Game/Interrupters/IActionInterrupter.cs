using DragonBoyAccountManager.Core.Game.Actions;
using DragonBoyAccountManager.Core.Game.Context;

namespace DragonBoyAccountManager.Core.Game.Interrupters;

public interface IActionInterrupter
{
    string Name { get; }
    int Priority { get; }
    bool ShouldInterrupt(IGameContext context);
    Task<ActionResult> HandleAsync(IGameContext context, CancellationToken cancellationToken = default);
}
