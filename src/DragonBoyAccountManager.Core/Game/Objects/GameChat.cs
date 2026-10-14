using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Objects;

public enum ChatType
{
    Map = 0,    // Chat thường / khu vực
    Global = 1  // Chat kênh thế giới
}

public class GameChat(MonoReflector reflector) : GameObject(reflector)
{
    public override string ClassName => "Service";

    public bool SendChat(string text, ChatType type = ChatType.Map)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            IntPtr service = Reflector.CallMethod("Service", "gI", IntPtr.Zero);
            if (service == IntPtr.Zero)
            {
                service = Reflector.ReadStatic<IntPtr>("Service", "instance");
            }

            if (service == IntPtr.Zero) return false;

            if (type == ChatType.Global)
            {
                Reflector.CallMethod("Service", "chatGlobal", service, text);
            }
            else
            {
                Reflector.CallMethod("Service", "chat", service, text);
            }

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GameChat] Lỗi gửi chat: {ex.Message}");
            return false;
        }
    }
}
