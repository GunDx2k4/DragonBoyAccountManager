using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameServerListScreen(MonoReflector reflector) : GameScreen(reflector)
{
    public override string ClassName => "ServerListScreen";

    public override IntPtr Pointer
    {
        get
        {
            IntPtr ptr = ReadStatic<IntPtr>("GameCanvas", "serverScreen");
            return ptr != IntPtr.Zero ? ptr : ReadStatic<IntPtr>("GameCanvas", "serverScr");
        }
    }

    public int IpSelect
    {
        get => ReadStatic<int>("ipSelect");
        set => WriteStatic("ipSelect", value);
    }

    public void SetIpSelect(int index, bool isSave = true)
    {
        CallStaticMethod("SetIpSelect", index, isSave);
    }

    public void SelectServer() => CallMethod("selectServer");

    public void SwitchToMe() => CallMethod("switchToMe");

    public void LoginNew() => CallMethod("Login_New");
}
