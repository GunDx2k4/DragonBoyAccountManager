using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Behaviors;

public enum BehaviorExecutionMode
{
    Exclusive = 0,
    Background = 1
}

public interface IBehavior
{
    string Id { get; }
    string Name { get; }
    BehaviorExecutionMode ExecutionMode { get; }
    int Priority { get; }
    bool IsRunning { get; }
    bool IsPaused { get; }

    Task StartAsync(ISessionContext context, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
    void Pause();
    void Resume();
    void Stop();
}

public interface IBehavior<TOptions> : IBehavior where TOptions : class
{
    TOptions Options { get; }
    void UpdateOptions(TOptions options);
}
