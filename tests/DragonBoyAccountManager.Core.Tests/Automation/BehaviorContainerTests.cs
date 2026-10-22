using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Automation.Behaviors.Navigation;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Tests.Automation;

public class BehaviorContainerTests
{
    private class DummyBehavior(string id, string name) : IBehavior
    {
        public string Id { get; } = id;
        public string Name { get; } = name;
        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }

        public Task StartAsync(ISessionContext context, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;
        public void Stop()
        {
            IsRunning = false;
            IsPaused = false;
        }
    }

    private sealed class DummyBehaviorA() : DummyBehavior("b1", "Behavior 1");
    private sealed class DummyBehaviorB() : DummyBehavior("b2", "Behavior 2");

    private sealed class StubSessionContext : ISessionContext
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public int ProcessId => 9999;
        public bool IsRunning => true;
        public bool IsQuitRequested => false;

        public PlayerSnapshot CurrentPlayer => PlayerSnapshot.Empty;
        public WorldSnapshot CurrentWorld => WorldSnapshot.Empty;
        public MapSnapshot CurrentMap => MapSnapshot.Empty;

        public ICommandDispatcher Dispatcher => throw new NotImplementedException();
        public AutoNavigator Navigator => throw new NotImplementedException();

        private readonly Dictionary<Type, ISnapshotScraper> _scrapers = [];
        private readonly Dictionary<Type, object> _snapshots = [];
        private readonly Dictionary<Type, IBehavior> _behaviors = [];

        public IEnumerable<IBehavior> Behaviors => _behaviors.Values;

        public void RegisterScraper<TSnapshot>(ISnapshotScraper<TSnapshot> scraper) where TSnapshot : class
        {
            _scrapers[typeof(TSnapshot)] = scraper;
        }

        public TSnapshot? GetSnapshot<TSnapshot>() where TSnapshot : class
        {
            return _snapshots.TryGetValue(typeof(TSnapshot), out var snap) ? snap as TSnapshot : null;
        }

        public void RegisterBehavior<TBehavior>(TBehavior behavior) where TBehavior : class, IBehavior
        {
            _behaviors[typeof(TBehavior)] = behavior;
        }

        public TBehavior? GetBehavior<TBehavior>() where TBehavior : class, IBehavior
        {
            return _behaviors.TryGetValue(typeof(TBehavior), out var b) ? b as TBehavior : null;
        }

        public IBehavior? GetBehavior(string behaviorId)
        {
            return _behaviors.Values.FirstOrDefault(b => string.Equals(b.Id, behaviorId, StringComparison.OrdinalIgnoreCase));
        }

        public void StopAllBehaviors()
        {
            foreach (var b in _behaviors.Values)
            {
                b.Stop();
            }
        }

        public void Tick()
        {
            foreach (var (type, scraper) in _scrapers)
            {
                _snapshots[type] = scraper.Scrape();
            }
        }

        public void Kill() { }
    }

    private sealed record DummySnapshot(int Value);

    private sealed class DummyScraper(int value) : ISnapshotScraper<DummySnapshot>
    {
        public DummySnapshot Scrape() => new(value);
    }

    [Fact]
    public void RegisterScraper_And_GetSnapshot_ScrapesAndStoresTypedSnapshot()
    {
        var context = new StubSessionContext();
        context.RegisterScraper(new DummyScraper(42));

        Assert.Null(context.GetSnapshot<DummySnapshot>());

        context.Tick();

        var snapshot = context.GetSnapshot<DummySnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(42, snapshot.Value);
    }

    [Fact]
    public void RegisterBehavior_And_GetBehavior_RetrievesCorrectInstance()
    {
        var context = new StubSessionContext();
        var behavior = new DummyBehaviorA();

        context.RegisterBehavior(behavior);

        var retrievedByType = context.GetBehavior<DummyBehaviorA>();
        var retrievedById = context.GetBehavior("b1");

        Assert.NotNull(retrievedByType);
        Assert.NotNull(retrievedById);
        Assert.Same(behavior, retrievedByType);
        Assert.Same(behavior, retrievedById);
        Assert.Single(context.Behaviors);
    }

    [Fact]
    public async Task StopAllBehaviors_StopsAllRunningBehaviors()
    {
        var context = new StubSessionContext();
        var b1 = new DummyBehaviorA();
        var b2 = new DummyBehaviorB();

        context.RegisterBehavior(b1);
        context.RegisterBehavior(b2);

        await b1.StartAsync(context);
        await b2.StartAsync(context);

        Assert.True(b1.IsRunning);
        Assert.True(b2.IsRunning);

        context.StopAllBehaviors();

        Assert.False(b1.IsRunning);
        Assert.False(b2.IsRunning);
    }

    private sealed class LoopingTestBehavior() : AutomationBehavior("LoopTest", "LoopTest")
    {
        public int TickCount { get; private set; }

        protected override int TickDelayMs => 50;

        protected override Task OnTickAsync(
            PlayerSnapshot player,
            WorldSnapshot world,
            MapSnapshot map,
            ICommandDispatcher dispatcher,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            TickCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task AutomationBehavior_StopAndImmediateRestart_DoesNotAbortSecondRun()
    {
        var behavior = new LoopingTestBehavior();
        using var dispatcher = new CommandDispatcher();

        var firstRun = behavior.StartAsync(
            () => PlayerSnapshot.Empty,
            () => WorldSnapshot.Empty,
            () => MapSnapshot.Empty,
            dispatcher);

        await Task.Delay(20, CancellationToken.None);
        Assert.True(behavior.IsRunning);

        behavior.Stop();
        var secondRun = behavior.StartAsync(
            () => PlayerSnapshot.Empty,
            () => WorldSnapshot.Empty,
            () => MapSnapshot.Empty,
            dispatcher);

        await Task.Delay(30, CancellationToken.None);
        Assert.True(behavior.IsRunning);

        behavior.Stop();
        await Task.WhenAll(firstRun, secondRun);
        Assert.False(behavior.IsRunning);
    }
}
