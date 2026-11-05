using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public interface IInterrupter
{
    string Name { get; }

    Task<Result?> CheckAndExecuteAsync(
        ISessionContext context,
        CancellationToken cancellationToken = default);
}
