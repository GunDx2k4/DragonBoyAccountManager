namespace DragonBoyAccountManager.GUI.Services.Dispatching;

public interface IDispatcherService
{
    void Invoke(Action action);
    T Invoke<T>(Func<T> func);
    Task InvokeAsync(Action action);
    Task<T> InvokeAsync<T>(Func<T> func);
    bool CheckAccess();
}

public static class DispatcherExtensions
{
    public static void Dispatch(this IDispatcherService? dispatcher, Action action)
    {
        if (dispatcher != null)
        {
            _ = dispatcher.InvokeAsync(action);
        }
        else if (App.Current?.Dispatcher != null)
        {
            _ = App.Current.Dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }
}
