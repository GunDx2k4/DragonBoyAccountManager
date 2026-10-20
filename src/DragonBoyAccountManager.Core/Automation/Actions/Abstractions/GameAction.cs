
namespace DragonBoyAccountManager.Core.Automation.Actions.Abstractions;

public abstract class GameAction(string name) : IGameAction
{
    private int _isRunning;
    private IGameContext? _currentContext;

    public string Name { get; } = name;
    public bool IsRunning => Volatile.Read(ref _isRunning) == 1;
    public IGameContext? CurrentContext => _currentContext;

    public async Task<ActionResult> ExecuteAsync(
        IGameContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (cancellationToken.IsCancellationRequested || !context.IsRunning)
        {
            return ActionResult.Failure("Hành động bị hủy hoặc game đã dừng.");
        }

        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return ActionResult.Failure($"Hành động '{Name}' đang trong quá trình thực thi trên một context khác.");
        }

        _currentContext = context;

        try
        {
            OnEnter(context);
            var result = await OnExecuteAsync(context, progress, cancellationToken);
            OnExit(context, result);
            return result;
        }
        catch (OperationCanceledException ex)
        {
            progress?.Report($"[{Name}] Đã bị hủy bỏ.");
            return ActionResult.Failure("Hành động bị hủy.", ex);
        }
        catch (Exception ex)
        {
            progress?.Report($"[{Name}] Lỗi: {ex.Message}");
            return ActionResult.Failure(ex.Message, ex);
        }
        finally
        {
            _currentContext = null;
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    protected virtual void OnEnter(IGameContext context) { }

    protected abstract Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    protected virtual void OnExit(IGameContext context, ActionResult result) { }
}

public abstract class GameAction<TOptions> : GameAction, IConfigurableAction<TOptions>
    where TOptions : class
{
    private volatile TOptions _options;

    public TOptions Options => _options;
    public object CurrentOptions => _options;

    public event Action<TOptions>? OptionsChanged;

    protected GameAction(string name, TOptions initialOptions) : base(name)
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

    void IConfigurableAction.UpdateOptions(object newOptions)
    {
        if (newOptions is TOptions typed)
        {
            UpdateOptions(typed);
        }
        else
        {
            throw new ArgumentException($"Options type must be {typeof(TOptions).FullName}", nameof(newOptions));
        }
    }

    protected virtual void OnOptionsChanged(TOptions previousOptions, TOptions newOptions) { }
}
