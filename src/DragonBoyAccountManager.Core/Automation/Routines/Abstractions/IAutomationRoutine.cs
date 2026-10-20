
namespace DragonBoyAccountManager.Core.Automation.Routines.Abstractions;

public interface IAutomationRoutine
{
    string Id { get; }
    string Name { get; }
    RoutinePriority Priority { get; }
    bool IsRunning { get; }
    bool IsPaused { get; }

    Func<IGameAction, IProgress<string>?, CancellationToken, Task<ActionResult>>? ActionExecutor { get; set; }

    Task StartAsync(
        IGameContext context,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    void Pause();
    void Resume();
    void Stop();
}

public interface IAutomationRoutine<TOptions> : IAutomationRoutine
    where TOptions : class
{
    TOptions Options { get; }
    void UpdateOptions(TOptions newOptions);
    event Action<TOptions>? OptionsChanged;
}
