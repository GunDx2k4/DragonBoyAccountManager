using System.Diagnostics;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;

namespace DragonBoyAccountManager.Core.Execution.Middlewares;

public class CommandLoggingMiddleware(Action<string>? logAction = null) : ICommandMiddleware
{
    private readonly Action<string>? _logAction = logAction;

    public async Task<Result> InvokeAsync(IGameCommand command, CommandExecutionDelegate next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next(command, cancellationToken);
            sw.Stop();

            if (_logAction != null)
            {
                string status = result.IsSuccess ? "[OK]" : "[FAIL]";
                _logAction($"{status} {command.Name} ({sw.ElapsedMilliseconds}ms) - {result.Message}");
            }

            return result;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logAction?.Invoke($"[ERROR] {command.Name} ({sw.ElapsedMilliseconds}ms) - Ngoại lệ: {ex.Message}");
            throw;
        }
    }
}
