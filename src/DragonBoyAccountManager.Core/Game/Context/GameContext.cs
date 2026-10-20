using DragonBoyAccountManager.Core.Game.Modules;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Context;

public interface IGameContext
{
    Guid ContextId { get; }

    IPlayerModule Player { get; }
    IWorldModule World { get; }
    IUIModule UI { get; }
    INetworkModule Network { get; }
    IEngineModule Engine { get; }

    MonoReflector Reflector { get; }

    bool IsRunning { get; }
    bool IsInGame { get; }
}

public class GameContext : IGameContext
{
    public Guid ContextId { get; }

    public IPlayerModule Player { get; }
    public IWorldModule World { get; }
    public IUIModule UI { get; }
    public INetworkModule Network { get; }
    public IEngineModule Engine { get; }

    public MonoReflector Reflector { get; }
    private readonly Func<bool> _isRunningEvaluator;

    public bool IsRunning => _isRunningEvaluator();
    public bool IsInGame => Player.IsInGame;

    public GameContext(MonoReflector reflector, Func<bool> isRunningEvaluator, Guid contextId = default)
    {
        ContextId = contextId == Guid.Empty ? Guid.NewGuid() : contextId;
        Reflector = reflector;
        _isRunningEvaluator = isRunningEvaluator;

        Player = new PlayerModule(reflector);
        UI = new UIModule(reflector);
        Network = new NetworkModule(reflector);
        Engine = new EngineModule(reflector);
        World = new WorldModule(reflector, UI, Network);
    }
}
