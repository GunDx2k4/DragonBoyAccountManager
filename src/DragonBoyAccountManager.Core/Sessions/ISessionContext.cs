using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Sessions;

public interface ISessionContext
{
    Guid SessionId { get; }
    int ProcessId { get; }
    bool IsRunning { get; }
    bool IsQuitRequested { get; }

    PlayerSnapshot CurrentPlayer { get; }
    WorldSnapshot CurrentWorld { get; }
    MapSnapshot CurrentMap { get; }

    ICommandDispatcher Dispatcher { get; }
    IMonoRuntime Mono { get; }

    IEnumerable<IBehavior> Behaviors { get; }
    TBehavior? GetBehavior<TBehavior>() where TBehavior : class, IBehavior;
    IBehavior? GetBehavior(string behaviorId);
    void RegisterBehavior<TBehavior>(TBehavior behavior) where TBehavior : class, IBehavior;
    void StopAllBehaviors();

    void Tick();
    void Kill();
}
