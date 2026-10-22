using DragonBoyAccountManager.Core.Automation.Interrupters;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors;

public abstract class AutomationBehavior : IBehavior
{
    private CancellationTokenSource? _internalCts;
    private Task? _runTask;
    private int _isRunning;
    private int _isPaused;

    public string Id { get; }
    public string Name { get; }

    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;
    public bool IsPaused => Volatile.Read(ref _isPaused) == 1;

    protected ISessionContext? Context { get; private set; }

    public EmergencyPipeline Interrupters { get; } = new();

    protected AutomationBehavior(string name, string? id = null)
    {
        Name = name;
        Id = id ?? name.Replace(" ", string.Empty);
    }

    public async Task StartAsync(
        ISessionContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Context = context;
        try
        {
            await StartAsync(
                () => context.CurrentPlayer,
                () => context.CurrentWorld,
                () => context.CurrentMap,
                context.Dispatcher,
                progress,
                cancellationToken);
        }
        finally
        {
            Context = null;
        }
    }

    public async Task StartAsync(
        Func<PlayerSnapshot> playerProvider,
        Func<WorldSnapshot> worldProvider,
        Func<MapSnapshot> mapProvider,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var existingTask = _runTask;
        var existingCts = _internalCts;
        if (existingTask is { IsCompleted: false } && existingCts is { IsCancellationRequested: true })
        {
            try { await existingTask; } catch { }
        }

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            return;

        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _internalCts = cts;

        var runTask = ExecuteLifecycleAsync(playerProvider, worldProvider, mapProvider, dispatcher, progress, cts);
        _runTask = runTask;
        await runTask;
    }

    private async Task ExecuteLifecycleAsync(
        Func<PlayerSnapshot> playerProvider,
        Func<WorldSnapshot> worldProvider,
        Func<MapSnapshot> mapProvider,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationTokenSource cts)
    {
        try
        {
            await RunLoopAsync(playerProvider, worldProvider, mapProvider, dispatcher, progress, cts.Token);
        }
        finally
        {
            Interlocked.CompareExchange(ref _internalCts, null, cts);
            cts.Dispose();
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

    private async Task RunLoopAsync(
        Func<PlayerSnapshot> playerProvider,
        Func<WorldSnapshot> worldProvider,
        Func<MapSnapshot> mapProvider,
        ICommandDispatcher dispatcher,
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

                var player = playerProvider();
                var world = worldProvider();
                var map = mapProvider();

                var interruptResult = await Interrupters.CheckAndExecuteAsync(Context, player, world, dispatcher, cancellationToken);
                if (interruptResult != null)
                {
                    progress?.Report($"[{Name}] Ngắt khẩn cấp: {interruptResult.Message}");
                    continue;
                }

                await OnTickAsync(Context, player, world, map, dispatcher, progress, cancellationToken);

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

        OnStopped(progress);
    }

    protected virtual int TickDelayMs => 100;

    protected virtual Task OnTickAsync(
        ISessionContext? context,
        PlayerSnapshot player,
        WorldSnapshot world,
        MapSnapshot map,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
        => OnTickAsync(player, world, map, dispatcher, progress, cancellationToken);

    protected abstract Task OnTickAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        MapSnapshot map,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    protected virtual void OnStarted(IProgress<string>? progress) { }
    protected virtual void OnStopped(IProgress<string>? progress) { }
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
