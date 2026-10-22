using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Automation.Behaviors.Navigation;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;
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

    void RegisterScraper<TSnapshot>(ISnapshotScraper<TSnapshot> scraper) where TSnapshot : class;
    TSnapshot? GetSnapshot<TSnapshot>() where TSnapshot : class;

    ICommandDispatcher Dispatcher { get; }
    AutoNavigator Navigator { get; }

    IEnumerable<IBehavior> Behaviors { get; }
    TBehavior? GetBehavior<TBehavior>() where TBehavior : class, IBehavior;
    IBehavior? GetBehavior(string behaviorId);
    void RegisterBehavior<TBehavior>(TBehavior behavior) where TBehavior : class, IBehavior;
    void StopAllBehaviors();

    void Tick();
    void Kill();
}
