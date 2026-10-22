using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;

namespace DragonBoyAccountManager.Core.Execution;

public class CommandDispatcher : ICommandDispatcher, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<Type, CommandExecutionDelegate> _handlers = new();
    private readonly List<ICommandMiddleware> _middlewares = [];

    public event Action<IGameCommand, Result>? CommandExecuted;
    public event Action<IGameCommand>? CommandExecuting;

    public void AddMiddleware(ICommandMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        lock (_middlewares)
        {
            _middlewares.Add(middleware);
        }
    }

    public void RegisterHandler<TCommand>(IGameCommandHandler<TCommand> handler) where TCommand : IGameCommand
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handlers[typeof(TCommand)] = (cmd, ct) => handler.HandleAsync((TCommand)cmd, ct);
    }

    public async Task<Result> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : IGameCommand
    {
        if (command == null) return Result.Failure("Lệnh không thể là null.");

        var commandType = command.GetType();
        if (!_handlers.TryGetValue(commandType, out var handlerDelegate))
        {
            return Result.Failure($"Không tìm thấy handler cho lệnh {commandType.Name}");
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            CommandExecuting?.Invoke(command);

            CommandExecutionDelegate pipeline = handlerDelegate;
            lock (_middlewares)
            {
                for (int i = _middlewares.Count - 1; i >= 0; i--)
                {
                    var current = _middlewares[i];
                    var next = pipeline;
                    pipeline = (cmd, ct) => current.InvokeAsync(cmd, next, ct);
                }
            }

            var result = await pipeline(command, cancellationToken);
            CommandExecuted?.Invoke(command, result);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            var res = Result.Failure($"Lệnh '{command.Name}' đã bị hủy.", ex);
            CommandExecuted?.Invoke(command, res);
            return res;
        }
        catch (Exception ex)
        {
            var res = Result.Failure($"Lỗi thực thi lệnh '{command.Name}': {ex.Message}", ex);
            CommandExecuted?.Invoke(command, res);
            return res;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
