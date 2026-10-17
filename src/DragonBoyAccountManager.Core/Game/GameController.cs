using DragonBoyAccountManager.Core.Game.Actions;

namespace DragonBoyAccountManager.Core.Game;

public class GameController(GameInstance instance) : IDisposable
{
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly object _periodicLock = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _isPaused;

    public GameInstance Instance { get; } = instance;
    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;
    public bool IsPaused => _isPaused;

    public IGameAction? CurrentAction { get; private set; }
    public bool IsExecutingAction => CurrentAction != null;

    public event Action<IGameAction?>? ActionChanged;
    public event Action<GameInstance>? Ticked;
    public event Action<Exception>? ErrorOccurred;

    public List<IGameAction> PeriodicActions { get; } = [];

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

            while (Instance.IsRunning && !token.IsCancellationRequested)
            {
                try
                {
                    if (!await timer.WaitForNextTickAsync(token))
                        break;

                    if (!Instance.IsRunning || token.IsCancellationRequested)
                        break;

                    if (_isPaused || IsExecutingAction) continue;

                    IGameAction[] actions;
                    lock (_periodicLock)
                    {
                        actions = [.. PeriodicActions];
                    }

                    foreach (var action in actions)
                    {
                        if (token.IsCancellationRequested || !Instance.IsRunning) break;
                        await action.ExecuteAsync(Instance.Context, null, token);
                    }

                    if (!Instance.IsRunning || token.IsCancellationRequested)
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

    public void Stop()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            try
            {
                _loopTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException) { }
            catch (OperationCanceledException) { }

            _cts.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    public void Dispose()
    {
        Stop();
        _actionLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
