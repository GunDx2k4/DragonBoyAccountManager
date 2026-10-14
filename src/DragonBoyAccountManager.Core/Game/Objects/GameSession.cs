using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameSession(MonoReflector reflector) : GameObject(reflector)
{
    public override string ClassName => "Session_ME";

    public bool IsConnected => ReadStatic<bool>("connected");
    public bool IsConnecting => ReadStatic<bool>("connecting");
    public bool IsKeyComplete => ReadStatic<bool>("getKeyComplete");
}
