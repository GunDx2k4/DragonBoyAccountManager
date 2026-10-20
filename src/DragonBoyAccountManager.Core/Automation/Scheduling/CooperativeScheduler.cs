using DragonBoyAccountManager.Core.Automation.Actions.Abstractions;
using DragonBoyAccountManager.Core.Automation.Routines.Abstractions;
using DragonBoyAccountManager.Core.Domain.Contracts;
using DragonBoyAccountManager.Core.Domain.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Scheduling;

public class CooperativeScheduler
{
    private readonly RoutineCoordinator _coordinator;
    private readonly object _tickLock = new();

    public RoutineCoordinator Coordinator => _coordinator;

    public CooperativeScheduler(RoutineCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public IReadOnlyList<IAutomationRoutine> GetActiveRoutinesInPriorityOrder()
    {
        return _coordinator.ActiveRoutines;
    }

    public async Task<ActionResult?> EvaluateTickAsync(
        IGameContext context,
        PlayerSnapshot playerSnapshot,
        WorldSnapshot worldSnapshot,
        CancellationToken cancellationToken = default)
    {
        var activeRoutines = GetActiveRoutinesInPriorityOrder();
        if (activeRoutines.Count == 0)
            return null;

        foreach (var routine in activeRoutines)
        {
            if (cancellationToken.IsCancellationRequested || !context.IsRunning)
                break;

            if (routine.IsPaused)
                continue;

            return null;
        }

        return null;
    }
}
