using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Game.Actions;
using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Routines.Abstractions;

namespace DragonBoyAccountManager.Core.Game.Routines.Coordination;

public class RoutineCoordinator(
    IGameContext context,
    Func<IGameAction, IProgress<string>?, CancellationToken, Task<ActionResult>> actionExecutor) : IDisposable
{
    private readonly IGameContext _context = context ?? throw new ArgumentNullException(nameof(context));
    private readonly Func<IGameAction, IProgress<string>?, CancellationToken, Task<ActionResult>> _actionExecutor =
        actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));

    private readonly ConcurrentDictionary<string, IAutomationRoutine> _routines = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningCts = new();
    private readonly ConcurrentDictionary<string, Task> _runningTasks = new();
    private readonly object _stateLock = new();
    private bool _isDisposed;

    public IGameContext Context => _context;

    public IReadOnlyList<IAutomationRoutine> RegisteredRoutines => _routines.Values.ToList();

    public IReadOnlyList<IAutomationRoutine> ActiveRoutines =>
        _routines.Values.Where(r => r.IsRunning).OrderByDescending(r => r.Priority).ToList();

    public bool IsAnyRoutineRunning => _routines.Values.Any(r => r.IsRunning);

    public event Action<IAutomationRoutine, bool>? RoutineStateChanged;
    public event Action<IAutomationRoutine, Exception>? RoutineErrorOccurred;

    public void Register(IAutomationRoutine routine)
    {
        ArgumentNullException.ThrowIfNull(routine);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _routines.AddOrUpdate(routine.Id, routine, (_, _) => routine);
    }

    public void Unregister(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        Stop(id);
        _routines.TryRemove(id, out _);
    }

    public void Unregister<T>() where T : class, IAutomationRoutine
    {
        var existing = Get<T>();
        if (existing != null)
        {
            Unregister(existing.Id);
        }
    }

    public T? Get<T>() where T : class, IAutomationRoutine
    {
        return _routines.Values.OfType<T>().FirstOrDefault();
    }

    public IAutomationRoutine? Get(string id)
    {
        return _routines.TryGetValue(id, out var routine) ? routine : null;
    }

    public bool IsRunning(string id)
    {
        return _routines.TryGetValue(id, out var routine) && routine.IsRunning;
    }

    public bool IsRunning<T>() where T : class, IAutomationRoutine
    {
        var routine = Get<T>();
        return routine != null && routine.IsRunning;
    }

    public Task StartAsync(
        IAutomationRoutine routine,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(routine);
        Register(routine);
        return StartAsync(routine.Id, progress, cancellationToken);
    }

    public Task StartAsync<T>(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default) where T : class, IAutomationRoutine
    {
        var routine = Get<T>();
        if (routine == null)
        {
            throw new InvalidOperationException($"Routine của kiểu {typeof(T).Name} chưa được đăng ký.");
        }
        return StartAsync(routine.Id, progress, cancellationToken);
    }

    public Task StartAsync(
        string id,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!_routines.TryGetValue(id, out var routine))
        {
            throw new KeyNotFoundException($"Không tìm thấy routine với định danh '{id}'.");
        }

        lock (_stateLock)
        {
            StopInternal(id);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runningCts[id] = cts;

            routine.ActionExecutor = _actionExecutor;

            var task = Task.Run(async () =>
            {
                try
                {
                    RoutineStateChanged?.Invoke(routine, true);
                    await routine.StartAsync(_context, progress, cts.Token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    RoutineErrorOccurred?.Invoke(routine, ex);
                }
                finally
                {
                    routine.ActionExecutor = null;
                    _runningCts.TryRemove(id, out _);
                    _runningTasks.TryRemove(id, out _);
                    RoutineStateChanged?.Invoke(routine, false);
                }
            }, cts.Token);

            _runningTasks[id] = task;
            return task;
        }
    }

    public void Pause(string id)
    {
        if (_routines.TryGetValue(id, out var routine))
        {
            routine.Pause();
        }
    }

    public void Pause<T>() where T : class, IAutomationRoutine
    {
        Get<T>()?.Pause();
    }

    public void Resume(string id)
    {
        if (_routines.TryGetValue(id, out var routine))
        {
            routine.Resume();
        }
    }

    public void Resume<T>() where T : class, IAutomationRoutine
    {
        Get<T>()?.Resume();
    }

    public void Stop(string id)
    {
        lock (_stateLock)
        {
            StopInternal(id);
        }
    }

    public void Stop<T>() where T : class, IAutomationRoutine
    {
        var routine = Get<T>();
        if (routine != null)
        {
            Stop(routine.Id);
        }
    }

    public void StopAll()
    {
        lock (_stateLock)
        {
            foreach (var id in _routines.Keys.ToList())
            {
                StopInternal(id);
            }
        }
    }

    private void StopInternal(string id)
    {
        if (_runningCts.TryRemove(id, out var cts))
        {
            try { cts.Cancel(); } catch { }
            cts.Dispose();
        }

        if (_routines.TryGetValue(id, out var routine))
        {
            routine.Stop();
            routine.ActionExecutor = null;
        }

        _runningTasks.TryRemove(id, out _);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        StopAll();
        _routines.Clear();
        GC.SuppressFinalize(this);
    }
}
