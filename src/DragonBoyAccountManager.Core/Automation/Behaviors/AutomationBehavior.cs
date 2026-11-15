using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Behaviors;

public abstract class AutomationBehavior : IBehavior
{
    private CancellationTokenSource? _internalCts;
    private CancellationToken _hostCancellationToken;
    private Task? _runTask;
    private long _stopVersion;
    private int _isRunning;
    private int _isPaused;

    public string Id { get; }
    public string Name { get; }
    public virtual BehaviorExecutionMode ExecutionMode => BehaviorExecutionMode.Exclusive;
    public virtual int Priority => 0;

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;
    public bool IsPaused => Volatile.Read(ref _isPaused) == 1;
    public bool IsStopping => Volatile.Read(ref _internalCts) is { IsCancellationRequested: true };

    protected ISessionContext? Context { get; private set; }
    protected CancellationToken HostCancellationToken => _hostCancellationToken;
    protected bool IsHostCancellationRequested => _hostCancellationToken.IsCancellationRequested;

    protected AutomationBehavior(string name, string? id = null)
    {
        Name = name;
        Id = id ?? name.Replace(" ", string.Empty);
    }

    public void AttachContext(ISessionContext context)
    {
        Context = context;
    }

    public async Task StartAsync(
        ISessionContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        long stopVersionAtEntry = Volatile.Read(ref _stopVersion);
        var existingTask = Volatile.Read(ref _runTask);
        var existingCts = Volatile.Read(ref _internalCts);
        if (existingTask is { IsCompleted: false } && existingCts is { IsCancellationRequested: true })
        {
            try { await existingTask.ConfigureAwait(false); } catch { }
        }

        if (Volatile.Read(ref _stopVersion) != stopVersionAtEntry || cancellationToken.IsCancellationRequested)
            return;

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            return;

        Context = context;
        _hostCancellationToken = cancellationToken;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Volatile.Write(ref _internalCts, cts);

        var runTask = ExecuteLifecycleAsync(context, progress, cts);
        Volatile.Write(ref _runTask, runTask);
        await runTask;
    }

    private async Task ExecuteLifecycleAsync(
        ISessionContext context,
        IProgress<string>? progress,
        CancellationTokenSource cts)
    {
        try
        {
            await RunLoopAsync(context, progress, cts.Token);
        }
        finally
        {
            Interlocked.Exchange(ref _isPaused, 0);
            Interlocked.CompareExchange(ref _internalCts, null, cts);
            cts.Dispose();
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    public void Pause() => Interlocked.Exchange(ref _isPaused, 1);
    public void Resume() => Interlocked.Exchange(ref _isPaused, 0);

    public void Stop()
    {
        Interlocked.Increment(ref _stopVersion);
        try { Volatile.Read(ref _internalCts)?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    public async Task StopAsync()
    {
        Stop();
        var task = Volatile.Read(ref _runTask);
        if (task != null)
        {
            try { await task.ConfigureAwait(false); } catch { }
        }
    }

    private async Task RunLoopAsync(
        ISessionContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        OnStarted(progress);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (IsPaused)
                {
                    await Task.Delay(200, cancellationToken);
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

        await OnStoppedAsync(progress).ConfigureAwait(false);
    }

    protected virtual int TickDelayMs => 100;

    protected abstract Task OnTickAsync(
        ISessionContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    protected virtual void OnStarted(IProgress<string>? progress) { }
    protected virtual void OnStopped(IProgress<string>? progress) { }

    protected virtual Task OnStoppedAsync(IProgress<string>? progress)
    {
        OnStopped(progress);
        return Task.CompletedTask;
    }
}

public abstract class AutomationBehavior<TOptions> : AutomationBehavior, IBehavior<TOptions>
    where TOptions : class
{
    private volatile TOptions _options;

    public TOptions Options => _options;
    public event Action<TOptions>? OptionsChanged;

    protected AutomationBehavior(string name, TOptions initialOptions, string? id = null)
        : base(name, id)
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
