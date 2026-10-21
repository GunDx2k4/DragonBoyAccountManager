namespace DragonBoyAccountManager.Core.Sessions;

public class SessionWatchdog : IDisposable
{
    private readonly PeriodicTimer _timer;
    private readonly CancellationTokenSource _cts = new();
    private Task? _monitorTask;

    public event Action<GameSession>? SessionCrashed;
    public event Action<GameSession>? SessionExited;

    public SessionWatchdog(TimeSpan checkInterval)
    {
        _timer = new PeriodicTimer(checkInterval);
    }

    public void StartMonitoring(GameSession session)
    {
        _monitorTask = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (!await _timer.WaitForNextTickAsync(_cts.Token))
                        break;

                    if (!session.IsRunning)
                    {
                        SessionExited?.Invoke(session);
                        break;
                    }

                    session.Tick();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception)
                {
                    SessionCrashed?.Invoke(session);
                }
            }
        }, _cts.Token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _timer.Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }
}
