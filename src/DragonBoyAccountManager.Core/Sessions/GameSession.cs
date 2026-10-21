using System.Diagnostics;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Automation.Behaviors.Navigation;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Handlers;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Sessions;

public sealed class GameSession : ISessionContext, IDisposable
{
    public Guid SessionId { get; }
    public Process Process { get; }
    public IMonoRuntime Mono { get; }

    public PlayerScraper PlayerScraper { get; }
    public WorldScraper WorldScraper { get; }
    public MapScraper MapScraper { get; }

    public CommandDispatcher Dispatcher { get; }
    ICommandDispatcher ISessionContext.Dispatcher => Dispatcher;
    public AutoNavigator Navigator { get; }

    public AutoTrainBehavior TrainBehavior { get; }
    public AutoChatBehavior ChatBehavior { get; }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, IBehavior> _behaviors = new();

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

    public PlayerSnapshot CurrentPlayer { get; private set; } = PlayerSnapshot.Empty;
    public WorldSnapshot CurrentWorld { get; private set; } = WorldSnapshot.Empty;
    public MapSnapshot CurrentMap { get; private set; } = MapSnapshot.Empty;

    public bool IsRunning => !Process.HasExited;
    public bool IsQuitRequested => CheckIsQuitRequested();
    public int ProcessId => Process.Id;

    private bool CheckIsQuitRequested()
    {
        if (Process.HasExited) return true;

        try
        {
            if (Mono.ReadStatic<bool>(GameSchema.Main.ClassName, GameSchema.Main.StaticIsQuitApp))
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

    public GameSession(Process process, IMonoRuntime mono, Guid sessionId = default)
    {
        SessionId = sessionId == default ? Guid.NewGuid() : sessionId;
        Process = process;
        Process.EnableRaisingEvents = true;
        Mono = mono;

        PlayerScraper = new PlayerScraper(mono);
        WorldScraper = new WorldScraper(mono);
        MapScraper = new MapScraper(mono);

        Dispatcher = new CommandDispatcher();

        var combatHandler = new CombatHandler(mono);
        var movementHandler = new MovementHandler(mono);
        var interactionHandler = new InteractionHandler(mono);
        var skillAssignmentHandler = new SkillAssignmentHandler(mono);

        Dispatcher.RegisterHandler<AttackMobCommand>(combatHandler);
        Dispatcher.RegisterHandler<SelectSkillCommand>(combatHandler);
        Dispatcher.RegisterHandler<MoveCommand>(movementHandler);
        Dispatcher.RegisterHandler<UseItemCommand>(interactionHandler);
        Dispatcher.RegisterHandler<PickItemCommand>(interactionHandler);
        Dispatcher.RegisterHandler<SendChatCommand>(interactionHandler);
        Dispatcher.RegisterHandler<WakeUpCommand>(interactionHandler);
        Dispatcher.RegisterHandler<ChangeZoneCommand>(interactionHandler);
        Dispatcher.RegisterHandler<LoginCommand>(interactionHandler);
        Dispatcher.RegisterHandler<AssignSkillsCommand>(skillAssignmentHandler);

        Navigator = new AutoNavigator(Dispatcher);

        TrainBehavior = new AutoTrainBehavior(new AutoTrainOptions());
        ChatBehavior = new AutoChatBehavior(new AutoChatOptions());
        RegisterBehavior(TrainBehavior);
        RegisterBehavior(ChatBehavior);
    }

    public void Tick()
    {
        if (!IsRunning || IsQuitRequested) return;

        CurrentPlayer = PlayerScraper.ScrapePlayer();
        CurrentWorld = WorldScraper.ScrapeWorld();
        CurrentMap = MapScraper.ScrapeMap();

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
        Mono.Dispose();
        GC.SuppressFinalize(this);
    }
}
