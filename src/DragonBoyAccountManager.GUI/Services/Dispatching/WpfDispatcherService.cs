using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DragonBoyAccountManager.GUI.Services.Dispatching;

public sealed class WpfDispatcherService : IDispatcherService
{
    private static Dispatcher UiDispatcher =>
        System.Windows.Application.Current?.Dispatcher
        ?? throw new InvalidOperationException("Application.Current is null or has no Dispatcher.");

    public void Invoke(Action action)
    {
        if (CheckAccess())
        {
            action();
        }
        else
        {
            UiDispatcher.Invoke(action);
        }
    }

    public T Invoke<T>(Func<T> func)
    {
        if (CheckAccess())
        {
            return func();
        }
        return UiDispatcher.Invoke(func);
    }

    public Task InvokeAsync(Action action)
    {
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return UiDispatcher.InvokeAsync(action).Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (CheckAccess())
        {
            return Task.FromResult(func());
        }
        return UiDispatcher.InvokeAsync(func).Task;
    }

    public bool CheckAccess() => UiDispatcher.CheckAccess();
}
