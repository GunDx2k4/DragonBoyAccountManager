using DragonBoyAccountManager.Core.Game.Actions;

namespace DragonBoyAccountManager.Core.Game;

public class GameController : IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly object _periodicLock = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _isPaused;

    public GameInstance Instance { get; }
    public RoutineCoordinator Routines { get; }

    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;
    public bool IsPaused => _isPaused;

    public IGameAction? CurrentAction { get; private set; }
    public bool IsExecutingAction => CurrentAction != null;
    public bool IsRoutineRunning => Routines.IsAnyRoutineRunning;

    public event Action<IGameAction?>? ActionChanged;
    public event Action<GameInstance>? Ticked;
    public event Action<Exception>? ErrorOccurred;

    public List<IGameAction> PeriodicActions { get; } = [];

    public GameController(GameInstance instance)
    {
        Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        Routines = new RoutineCoordinator(instance.Context, (action, prog, ct) => ExecuteAsync(action, prog, ct));
    }

    public void AddPeriodicAction(IGameAction action)
    {
        lock (_periodicLock)
        {
            PeriodicActions.Add(action);
        }
    }

    public void RemovePeriodicAction(IGameAction action)
    {
        lock (_periodicLock)
        {
            PeriodicActions.Remove(action);
        }
    }

    public void ClearPeriodicActions()
    {
        lock (_periodicLock)
        {
            PeriodicActions.Clear();
        }
    }

    public async Task<ActionResult> ExecuteAsync(
        IGameAction action,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _actionLock.WaitAsync(cancellationToken);
        CurrentAction = action;
        ActionChanged?.Invoke(action);
        try
        {
            return await action.ExecuteAsync(Instance.Context, progress, cancellationToken);
        }
        finally
        {
            CurrentAction = null;
            ActionChanged?.Invoke(null);
            _actionLock.Release();
        }
    }

    public void Start(TimeSpan interval)
    {
        Stop();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _isPaused = false;

        _loopTask = Task.Run(async () =>
        {
            using var timer = new PeriodicTimer(interval);

            while (Instance.IsRunning && !Instance.IsQuitRequested && !token.IsCancellationRequested)
            {
                try
                {
                    if (!await timer.WaitForNextTickAsync(token))
                        break;

                    if (!Instance.IsRunning || Instance.IsQuitRequested || token.IsCancellationRequested)
                        break;

                    if (_isPaused) continue;

                    if (!IsExecutingAction)
                    {
                        IGameAction[] actions;
                        lock (_periodicLock)
                        {
                            actions = [.. PeriodicActions];
                        }

                        foreach (var action in actions)
                        {
                            if (token.IsCancellationRequested || !Instance.IsRunning || Instance.IsQuitRequested) break;
                            await ExecuteAsync(action, null, token);
                        }
                    }

                    if (!Instance.IsRunning || Instance.IsQuitRequested || token.IsCancellationRequested)
                        break;

                    Ticked?.Invoke(Instance);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(ex);
                }
            }
        }, token);
    }

    public void Pause() => _isPaused = true;

    public void Resume() => _isPaused = false;

    public async Task StopAsync()
    {
        Routines.StopAll();

        if (_cts != null)
        {
            _cts.Cancel();
            if (_loopTask != null)
            {
                try
                {
                    await Task.WhenAny(_loopTask, Task.Delay(2000)).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }

            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    public void Stop()
    {
        Routines.StopAll();

        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        Routines.Dispose();
        _actionLock.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        Stop();
        Routines.Dispose();
        _actionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
