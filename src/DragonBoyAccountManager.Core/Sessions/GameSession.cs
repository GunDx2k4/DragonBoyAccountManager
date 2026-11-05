using System.Collections.Concurrent;
using System.Diagnostics;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Automation.Behaviors.Navigation;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Execution.Handlers;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Sessions.Controllers;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Sessions;

public sealed class GameSession : ISessionContext, IDisposable
{
    public Guid SessionId { get; }
    public Process Process { get; }
    public IGameRuntime Runtime { get; }

    public PlayerScraper PlayerScraper { get; }
    public WorldScraper WorldScraper { get; }
    public MapScraper MapScraper { get; }
    public SessionMaskingController MaskingController { get; }
    public LoginScreenController LoginScreen { get; }
    public GameSpeedController SpeedController { get; }

    public CommandDispatcher Dispatcher { get; }
    ICommandDispatcher ISessionContext.Dispatcher => Dispatcher;
    public AutoNavigator Navigator { get; }

    private readonly ConcurrentDictionary<Type, ISnapshotScraper> _scrapers = new();
    private readonly ConcurrentDictionary<Type, object> _snapshots = new();
    private readonly ConcurrentDictionary<Type, IBehavior> _behaviors = new();

    public void RegisterScraper<TSnapshot>(ISnapshotScraper<TSnapshot> scraper) where TSnapshot : class
    {
        ArgumentNullException.ThrowIfNull(scraper);
        _scrapers[typeof(TSnapshot)] = scraper;
    }

    public TSnapshot? GetSnapshot<TSnapshot>() where TSnapshot : class
    {
        return _snapshots.TryGetValue(typeof(TSnapshot), out var snap) ? snap as TSnapshot : null;
    }

    public void RegisterBehavior<TBehavior>(TBehavior behavior) where TBehavior : class, IBehavior
    {
        ArgumentNullException.ThrowIfNull(behavior);
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

    public PlayerSnapshot CurrentPlayer => GetSnapshot<PlayerSnapshot>() ?? PlayerSnapshot.Empty;
    public WorldSnapshot CurrentWorld => GetSnapshot<WorldSnapshot>() ?? WorldSnapshot.Empty;
    public MapSnapshot CurrentMap => GetSnapshot<MapSnapshot>() ?? MapSnapshot.Empty;

    public GameSessionSnapshot CreateSessionSnapshot()
        => new(CurrentPlayer, CurrentWorld, new Dictionary<Type, object>(_snapshots));

    public bool IsRunning => !Process.HasExited;
    public bool IsQuitRequested => CheckIsQuitRequested();
    public int ProcessId => Process.Id;

    private bool CheckIsQuitRequested()
    {
        if (Process.HasExited) return true;

        try
        {
            if (Runtime.ReadStatic<bool>(GameSchema.Main.ClassName, Runtime.Layout.Main.IsQuitApp))
                return true;
        }
        catch
        {
            return false;
        }

        return false;
    }

    public event Action<GameSession>? Ticked;
    public event EventHandler? Exited
    {
        add => Process.Exited += value;
        remove => Process.Exited -= value;
    }

    public GameSession(Process process, IGameRuntime runtime, Guid sessionId = default, Action<GameSession>? configure = null)
    {
        SessionId = sessionId == default ? Guid.NewGuid() : sessionId;
        Process = process;
        Process.EnableRaisingEvents = true;
        Runtime = runtime;

        MaskingController = new SessionMaskingController(runtime);
        LoginScreen = new LoginScreenController(runtime);
        SpeedController = new GameSpeedController(runtime);
        PlayerScraper = new PlayerScraper(runtime, () => MaskingController.OriginalCharacterName);
        WorldScraper = new WorldScraper(runtime);
        MapScraper = new MapScraper(runtime);

        RegisterScraper(PlayerScraper);
        RegisterScraper(WorldScraper);
        RegisterScraper(MapScraper);

        _snapshots[typeof(PlayerSnapshot)] = PlayerSnapshot.Empty;
        _snapshots[typeof(WorldSnapshot)] = WorldSnapshot.Empty;
        _snapshots[typeof(MapSnapshot)] = MapSnapshot.Empty;

        Dispatcher = new CommandDispatcher();
        Navigator = new AutoNavigator(Dispatcher);

        var combatHandler = new CombatHandler(runtime);
        var movementHandler = new MovementHandler(runtime, Navigator, this);
        var inventoryHandler = new InventoryHandler(runtime);
        var zoneHandler = new ZoneHandler(runtime);
        var chatHandler = new ChatHandler(runtime);
        var worldInteractionHandler = new WorldInteractionHandler(runtime);
        var skillAssignmentHandler = new SkillAssignmentHandler(runtime);

        Dispatcher.RegisterHandler<AttackMobCommand>(combatHandler);
        Dispatcher.RegisterHandler<SelectSkillCommand>(combatHandler);
        Dispatcher.RegisterHandler<SetInGameAutoTrainCommand>(combatHandler);
        Dispatcher.RegisterHandler<MoveCommand>(movementHandler);
        Dispatcher.RegisterHandler<NavigateCommand>(movementHandler);
        Dispatcher.RegisterHandler<EnterWaypointCommand>(movementHandler);
        Dispatcher.RegisterHandler<UseItemCommand>(inventoryHandler);
        Dispatcher.RegisterHandler<PickItemCommand>(inventoryHandler);
        Dispatcher.RegisterHandler<SendChatCommand>(chatHandler);
        Dispatcher.RegisterHandler<WakeUpCommand>(worldInteractionHandler);
        Dispatcher.RegisterHandler<ChangeZoneCommand>(zoneHandler);
        Dispatcher.RegisterHandler<OpenZonePanelCommand>(zoneHandler);
        Dispatcher.RegisterHandler<CloseZonePanelCommand>(zoneHandler);
        Dispatcher.RegisterHandler<LoginCommand>(worldInteractionHandler);
        Dispatcher.RegisterHandler<FocusEntityCommand>(worldInteractionHandler);
        Dispatcher.RegisterHandler<AssignSkillsCommand>(skillAssignmentHandler);

        RegisterBehavior(new AutoTrainBehavior(new AutoTrainOptions()));
        RegisterBehavior(new AutoChatBehavior(new AutoChatOptions()));

        configure?.Invoke(this);
    }

    public void Tick()
    {
        if (!IsRunning || IsQuitRequested) return;

        foreach (var (snapshotType, scraper) in _scrapers)
        {
            _snapshots[snapshotType] = scraper.Scrape();
        }

        bool isInGame = CurrentPlayer.IsInGame;
        SpeedController.OnTick(isInGame);
        MaskingController.OnTick(isInGame);

        Ticked?.Invoke(this);
    }

    public void Kill()
    {
        if (!Process.HasExited)
        {
            try
            {
                Process.Kill();
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
            }
        }
    }

    public IEnumerable<IBehavior> Behaviors => _behaviors.Values;

    public void StopAllBehaviors()
    {
        foreach (var behavior in _behaviors.Values)
        {
            try { behavior.Stop(); } catch { }
        }
    }

    public void Dispose()
    {
        StopAllBehaviors();
        Dispatcher.Dispose();
        Runtime.Dispose();
        GC.SuppressFinalize(this);
    }
}
