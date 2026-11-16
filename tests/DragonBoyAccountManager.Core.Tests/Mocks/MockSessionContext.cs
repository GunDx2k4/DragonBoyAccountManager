using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Automation.Navigation;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Tests.Mocks;

public sealed class MockSessionContext : ISessionContext
{
    private readonly List<IBehavior> _behaviors = [];
    private readonly Dictionary<Type, object> _scrapers = [];
    private readonly Dictionary<Type, object> _snapshots = [];

    public Guid SessionId { get; set; } = Guid.NewGuid();
    public int ProcessId { get; set; } = 12345;
    public bool IsRunning { get; set; } = true;
    public bool IsQuitRequested { get; set; } = false;

    public IGameRuntime Runtime { get; set; }
    public MockCommandDispatcher MockDispatcher { get; }
    public ICommandDispatcher Dispatcher => MockDispatcher;
    public AutoNavigator Navigator { get; }

    public PlayerSnapshot CurrentPlayer { get; set; } = PlayerSnapshot.Empty;
    public WorldSnapshot CurrentWorld { get; set; } = WorldSnapshot.Empty;
    public MapSnapshot CurrentMap { get; set; } = MapSnapshot.Empty;

    public IEnumerable<IBehavior> Behaviors => _behaviors;

    public MockSessionContext(IGameRuntime? runtime = null)
    {
        Runtime = runtime ?? new MockGameRuntime();
        MockDispatcher = new MockCommandDispatcher();
        Navigator = new AutoNavigator(MockDispatcher);
    }

    public void RegisterScraper<TSnapshot>(ISnapshotScraper<TSnapshot> scraper) where TSnapshot : class
    {
        _scrapers[typeof(TSnapshot)] = scraper;
    }

    public void SetSnapshot<TSnapshot>(TSnapshot snapshot) where TSnapshot : class
    {
        _snapshots[typeof(TSnapshot)] = snapshot;
        if (snapshot is PlayerSnapshot p) CurrentPlayer = p;
        if (snapshot is WorldSnapshot w) CurrentWorld = w;
        if (snapshot is MapSnapshot m) CurrentMap = m;
    }

    public TSnapshot? GetSnapshot<TSnapshot>() where TSnapshot : class
    {
        if (_snapshots.TryGetValue(typeof(TSnapshot), out var obj) && obj is TSnapshot typed)
            return typed;

        if (typeof(TSnapshot) == typeof(PlayerSnapshot)) return CurrentPlayer as TSnapshot;
        if (typeof(TSnapshot) == typeof(WorldSnapshot)) return CurrentWorld as TSnapshot;
        if (typeof(TSnapshot) == typeof(MapSnapshot)) return CurrentMap as TSnapshot;

        return null;
    }

    public TBehavior? GetBehavior<TBehavior>() where TBehavior : class, IBehavior
    {
        return _behaviors.OfType<TBehavior>().FirstOrDefault();
    }

    public IBehavior? GetBehavior(string behaviorId)
    {
        return _behaviors.FirstOrDefault(b => string.Equals(b.Id, behaviorId, StringComparison.OrdinalIgnoreCase));
    }

    public void RegisterBehavior<TBehavior>(TBehavior behavior) where TBehavior : class, IBehavior
    {
        _behaviors.Add(behavior);
    }

    public void StopAllBehaviors()
    {
        foreach (var behavior in _behaviors)
        {
            behavior.StopAsync().GetAwaiter().GetResult();
        }
    }

    public async Task StopAllBehaviorsAsync()
    {
        foreach (var behavior in _behaviors)
        {
            await behavior.StopAsync();
        }
    }

    public void Tick(bool force = false)
    {
    }

    public void Kill()
    {
        IsRunning = false;
    }
}
