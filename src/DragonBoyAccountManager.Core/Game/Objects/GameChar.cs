using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameChar(MonoReflector reflector) : GameObject(reflector)
{
    public override string ClassName => "Char";
    public override IntPtr Pointer => ReadStatic<IntPtr>("myChar");

    public string Name => IsAlive ? ReadFieldString("cName") : string.Empty;
    public long Power => IsInGame ? ReadField<long>("cPower") : 0;
    public long Hp => IsInGame ? ReadField<long>("cHP") : 0;
    public int Gender => IsInGame ? ReadField<int>("cgender") : 0;
    public int X => IsInGame ? ReadField<int>("cx") : 0;
    public int Y => IsInGame ? ReadField<int>("cy") : 0;
    public bool IsInGame => IsAlive && !string.IsNullOrWhiteSpace(Name);
}
