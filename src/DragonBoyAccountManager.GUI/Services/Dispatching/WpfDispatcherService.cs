using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace DragonBoyAccountManager.GUI.Services.Dispatching;

public sealed class WpfDispatcherService : IDispatcherService
{
    private readonly Dispatcher? _dispatcher;

    public WpfDispatcherService(Dispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher
            ?? System.Windows.Application.Current?.Dispatcher
            ?? Dispatcher.FromThread(Thread.CurrentThread);
    }

    private Dispatcher? UiDispatcher
    {
        get
        {
            if (_dispatcher is { HasShutdownStarted: false, HasShutdownFinished: false })
            {
                return _dispatcher;
            }

            var appDispatcher = System.Windows.Application.Current?.Dispatcher;
            if (appDispatcher is { HasShutdownStarted: false, HasShutdownFinished: false })
            {
                return appDispatcher;
            }

            return null;
        }
    }

    public void Invoke(Action action)
    {
        var dispatcher = UiDispatcher;
        if (dispatcher == null) return;

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action);
        }
    }

    public T Invoke<T>(Func<T> func)
    {
        var dispatcher = UiDispatcher;
        if (dispatcher == null) return default!;

        if (dispatcher.CheckAccess())
        {
            return func();
        }
        return dispatcher.Invoke(func);
    }

    public Task InvokeAsync(Action action)
    {
        var dispatcher = UiDispatcher;
        if (dispatcher == null) return Task.CompletedTask;

        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }
        return dispatcher.InvokeAsync(action).Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        var dispatcher = UiDispatcher;
        if (dispatcher == null) return Task.FromResult(default(T)!);

        if (dispatcher.CheckAccess())
        {
            return Task.FromResult(func());
        }
        return dispatcher.InvokeAsync(func).Task;
    }

    public bool CheckAccess()
    {
        var dispatcher = UiDispatcher;
        return dispatcher != null && dispatcher.CheckAccess();
    }
}
