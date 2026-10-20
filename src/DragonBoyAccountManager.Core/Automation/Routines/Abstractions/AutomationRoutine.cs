
namespace DragonBoyAccountManager.Core.Automation.Routines.Abstractions;

public abstract class AutomationRoutine : IAutomationRoutine
{
    private CancellationTokenSource? _internalCts;
    private Task? _runTask;
    private int _isRunning;
    private int _isPaused;

    public string Id { get; }
    public string Name { get; }
    public RoutinePriority Priority { get; }

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;
    public bool IsPaused => Volatile.Read(ref _isPaused) == 1;

    public Func<IGameAction, IProgress<string>?, CancellationToken, Task<ActionResult>>? ActionExecutor { get; set; }

    protected InterrupterPipeline Interrupters { get; } = new();

    protected AutomationRoutine(string name, RoutinePriority priority = RoutinePriority.Normal, string? id = null)
    {
        Name = name;
        Priority = priority;
        Id = id ?? name.Replace(" ", string.Empty);
    }

    public void RegisterInterrupter(IActionInterrupter interrupter) =>
        Interrupters.Register(interrupter);

    public async Task StartAsync(
        IGameContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            return;

        _internalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _internalCts.Token;

        try
        {
            _runTask = RunLoopAsync(context, progress, token);
            await _runTask;
        }
        finally
        {
            _internalCts?.Dispose();
            _internalCts = null;
            _runTask = null;
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    public void Pause() => Interlocked.Exchange(ref _isPaused, 1);

    public void Resume() => Interlocked.Exchange(ref _isPaused, 0);

    public void Stop()
    {
        try { _internalCts?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    protected Task<ActionResult> ExecuteActionAsync(
        IGameAction action,
        IGameContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (ActionExecutor != null)
        {
            return ActionExecutor(action, progress, cancellationToken);
        }
        return action.ExecuteAsync(context, progress, cancellationToken);
    }

    private async Task RunLoopAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        OnStarted(context, progress);

        while (!cancellationToken.IsCancellationRequested && context.IsRunning)
        {
            try
            {
                if (IsPaused)
                {
                    await Task.Delay(200, cancellationToken);
                    continue;
                }

                var interruptResult = await Interrupters.CheckAndExecuteAsync(context, cancellationToken);
                if (interruptResult != null)
                {
                    progress?.Report($"[{Name}] Interrupter: {interruptResult.Message}");
                    continue;
                }

                await OnTickAsync(context, progress, cancellationToken);

                if (TickDelayMs > 0 && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TickDelayMs, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                progress?.Report($"[{Name}] Ngoại lệ: {ex.Message}");
                await Task.Delay(500, cancellationToken);
            }
        }

        OnStopped(context, progress);
    }

    protected virtual int TickDelayMs => 100;

    protected abstract Task OnTickAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    protected virtual void OnStarted(IGameContext context, IProgress<string>? progress) { }

    protected virtual void OnStopped(IGameContext context, IProgress<string>? progress) { }
}

public abstract class AutomationRoutine<TOptions> : AutomationRoutine, IAutomationRoutine<TOptions>
    where TOptions : class
{
    private volatile TOptions _options;

    public TOptions Options => _options;

    public event Action<TOptions>? OptionsChanged;

    protected AutomationRoutine(
        string name,
        TOptions initialOptions,
        RoutinePriority priority = RoutinePriority.Normal,
        string? id = null)
        : base(name, priority, id)
    {
        _options = initialOptions ?? throw new ArgumentNullException(nameof(initialOptions));
    }

    public void UpdateOptions(TOptions newOptions)
    {
        ArgumentNullException.ThrowIfNull(newOptions);
        var previous = _options;
        _options = newOptions;
        OnOptionsChanged(previous, newOptions);
        OptionsChanged?.Invoke(newOptions);
    }

    protected virtual void OnOptionsChanged(TOptions previousOptions, TOptions newOptions) { }
}
