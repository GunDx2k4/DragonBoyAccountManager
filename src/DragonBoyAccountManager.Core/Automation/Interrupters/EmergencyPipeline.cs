using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public class EmergencyPipeline
{
    private readonly Lock _syncLock = new();
    private readonly List<IInterrupter> _interrupters = [];

    public void Register(IInterrupter interrupter)
    {
        ArgumentNullException.ThrowIfNull(interrupter);
        lock (_syncLock)
        {
            _interrupters.Add(interrupter);
        }
    }

    public async Task<Result?> CheckAndExecuteAsync(
        ISessionContext context,
        CancellationToken cancellationToken = default)
    {
        IInterrupter[] snapshot;
        lock (_syncLock)
        {
            snapshot = [.. _interrupters];
        }

        foreach (var interrupter in snapshot)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var result = await interrupter.CheckAndExecuteAsync(context, cancellationToken);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }
}
