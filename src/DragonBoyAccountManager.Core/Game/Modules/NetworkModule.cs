using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Modules;

public interface INetworkModule
{
    bool IsConnected { get; }
    bool IsConnecting { get; }
    bool IsKeyComplete { get; }

    void Connect();
    bool CharMove();
    bool RequestChangeZone(int zoneId, int index = -1);
    bool OpenUIZone();
    bool RequestChangeMap();
    bool GetMapOffline();
    bool UseItem(sbyte type, sbyte where, sbyte index, short templateId = -1);
    bool Chat(string text);
    bool ChatGlobal(string text);
    bool SendChat(string text, ChatType type = ChatType.Map);
}

public class NetworkModule(MonoReflector reflector) : INetworkModule
{
    private readonly MonoReflector _reflector = reflector;

    public bool IsConnected
    {
        get
        {
            try { return _reflector.ReadStatic<bool>(GameSchema.Session.ClassName, GameSchema.Session.StaticConnected); }
            catch { return false; }
        }
    }

    public bool IsConnecting
    {
        get
        {
            try { return _reflector.ReadStatic<bool>(GameSchema.Session.ClassName, GameSchema.Session.StaticConnecting); }
            catch { return false; }
        }
    }

    public bool IsKeyComplete
    {
        get
        {
            try { return _reflector.ReadStatic<bool>(GameSchema.Session.ClassName, GameSchema.Session.StaticGetKeyComplete); }
            catch { return false; }
        }
    }

    private IntPtr ServicePointer
    {
        get
        {
            try
            {
                IntPtr ptr = _reflector.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
                if (ptr == IntPtr.Zero)
                {
                    ptr = _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
                }
                return ptr;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }

    public void Connect()
    {
        try
        {
            _reflector.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodConnect, IntPtr.Zero);
        }
        catch { }
    }

    public bool CharMove()
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, service);
            return true;
        }
        catch { return false; }
    }

    public bool RequestChangeZone(int zoneId, int index = -1)
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodRequestChangeZone, service, zoneId, index);
            return true;
        }
        catch { return false; }
    }

    public bool OpenUIZone()
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodOpenUIZone, service);
            return true;
        }
        catch { return false; }
    }

    public bool RequestChangeMap()
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodRequestChangeMap, service);
            return true;
        }
        catch { return false; }
    }

    public bool GetMapOffline()
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGetMapOffline, service);
            return true;
        }
        catch { return false; }
    }

    public bool UseItem(sbyte type, sbyte where, sbyte index, short templateId = -1)
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodUseItem, service, type, where, index, templateId);
            return true;
        }
        catch { return false; }
    }

    public bool Chat(string text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodChat, service, text);
            return true;
        }
        catch { return false; }
    }

    public bool ChatGlobal(string text)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodChatGlobal, service, text);
            return true;
        }
        catch { return false; }
    }

    public bool SendChat(string text, ChatType type = ChatType.Map)
        => type == ChatType.Global ? ChatGlobal(text) : Chat(text);
}
