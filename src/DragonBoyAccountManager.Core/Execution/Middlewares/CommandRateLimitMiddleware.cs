using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;

namespace DragonBoyAccountManager.Core.Execution.Middlewares;

public class CommandRateLimitMiddleware(TimeSpan minInterval) : ICommandMiddleware
{
    private readonly TimeSpan _minInterval = minInterval;
    private readonly ConcurrentDictionary<Type, DateTime> _lastExecutionTime = new();

    public async Task<Result> InvokeAsync(IGameCommand command, CommandExecutionDelegate next, CancellationToken cancellationToken)
    {
        var commandType = command.GetType();
        if (_lastExecutionTime.TryGetValue(commandType, out var lastTime))
        {
            var elapsed = DateTime.UtcNow - lastTime;
            if (elapsed < _minInterval)
            {
                var delay = _minInterval - elapsed;
                await Task.Delay(delay, cancellationToken);
            }
        }

        var result = await next(command, cancellationToken);
        _lastExecutionTime[commandType] = DateTime.UtcNow;
        return result;
    }
}
