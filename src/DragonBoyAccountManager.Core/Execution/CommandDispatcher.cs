using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;

namespace DragonBoyAccountManager.Core.Execution;

public class CommandDispatcher : ICommandDispatcher, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly AsyncLocal<int> _lockDepth = new();
    private readonly ConcurrentDictionary<Type, Func<IGameCommand, CancellationToken, Task<Result>>> _handlers = new();

    public event Action<IGameCommand, Result>? CommandExecuted;
    public event Action<IGameCommand>? CommandExecuting;

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

        bool acquired = false;
        if (_lockDepth.Value == 0)
        {
            await _lock.WaitAsync(cancellationToken);
            acquired = true;
        }

        _lockDepth.Value++;
        try
        {
            CommandExecuting?.Invoke(command);

            var result = await handlerDelegate(command, cancellationToken);
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
            _lockDepth.Value--;
            if (acquired)
            {
                _lock.Release();
            }
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }
}
