using DragonBoyAccountManager.Core.Engine.Events;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface IGameEngineEvents
{
    event Action<SessionEvent>? OnSessionEvent;
}
