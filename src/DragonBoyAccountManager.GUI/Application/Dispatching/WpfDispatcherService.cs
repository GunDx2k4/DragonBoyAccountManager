using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using DragonBoyAccountManager.GUI.Application.Abstractions;

namespace DragonBoyAccountManager.GUI.Application.Dispatching;

public sealed class WpfDispatcherService : IDispatcherService
{
    private static Dispatcher CurrentDispatcher =>
        System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

    public void Invoke(Action action)
    {
        if (CheckAccess())
        {
            action();
        }
        else
        {
            CurrentDispatcher.Invoke(action);
        }
    }

    public T Invoke<T>(Func<T> func)
    {
        if (CheckAccess())
        {
            return func();
        }
        return CurrentDispatcher.Invoke(func);
    }

    public Task InvokeAsync(Action action)
    {
        if (CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return CurrentDispatcher.InvokeAsync(action).Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        if (CheckAccess())
        {
            return Task.FromResult(func());
        }
        return CurrentDispatcher.InvokeAsync(func).Task;
    }

    public bool CheckAccess() => CurrentDispatcher.CheckAccess();
}
