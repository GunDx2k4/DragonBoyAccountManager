using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameMidlet(MonoReflector reflector) : GameObject(reflector)
{
    public override string ClassName => "GameMidlet";

    public string Version => ReadStaticString("VERSION");
    public int IntVersion => ReadStatic<int>("intVERSION");
    public string IP => ReadStaticString("IP");
    public int Port => ReadStatic<int>("PORT");
}
