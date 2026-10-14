using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public abstract class GameScreen(MonoReflector reflector) : GameObject(reflector)
{
    public virtual void Perform(int actionId)
    {
        if (!IsAlive) return;
        string actualClass = Reflector.GetClassName(Pointer) ?? ClassName;
        CallMethod(actualClass, "perform", actionId, null);
    }
}
