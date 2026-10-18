using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Modules;

public interface INetworkModule
{
    bool IsConnected { get; }
    bool IsConnecting { get; }
    bool IsKeyComplete { get; }
    string? CurrentHost { get; }
    int CurrentPort { get; }

    string[]? ServerNames { get; }
    string[]? ServerAddresses { get; }
    short[]? ServerPorts { get; }
    int SelectedServerId { get; set; }
    void SelectServer(int serverId);

    void Connect();
    void Connect(string host, int port);
    void Close();
    bool SetClientType();
    bool Login(string username, string password, string? version = null, sbyte type = 0);
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

    public string? CurrentHost
    {
        get
        {
            try
            {
                IntPtr ipStr = _reflector.ReadStatic<IntPtr>(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticIp);
                if (ipStr != IntPtr.Zero) return _reflector.ReadMonoString(ipStr);
            }
            catch { }
            return null;
        }
    }

    public int CurrentPort
    {
        get
        {
            try { return _reflector.ReadStatic<int>(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticPort); }
            catch { return 0; }
        }
    }

    public string[]? ServerNames
    {
        get
        {
            try
            {
                IntPtr arrPtr = _reflector.ReadStatic<IntPtr>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticNameServer);
                if (arrPtr != IntPtr.Zero)
                {
                    return _reflector.ReadMonoStringArray(arrPtr);
                }
            }
            catch { }
            return null;
        }
    }

    public string[]? ServerAddresses
    {
        get
        {
            try
            {
                IntPtr arrPtr = _reflector.ReadStatic<IntPtr>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticAddress);
                if (arrPtr != IntPtr.Zero)
                {
                    return _reflector.ReadMonoStringArray(arrPtr);
                }
            }
            catch { }
            return null;
        }
    }

    public short[]? ServerPorts
    {
        get
        {
            try
            {
                IntPtr arrPtr = _reflector.ReadStatic<IntPtr>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticPort);
                if (arrPtr != IntPtr.Zero)
                {
                    return _reflector.ReadMonoShortArray(arrPtr);
                }
            }
            catch { }
            return null;
        }
    }

    public int SelectedServerId
    {
        get
        {
            try { return _reflector.ReadStatic<int>(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect); }
            catch { return -1; }
        }
        set
        {
            try { _reflector.WriteStatic(GameSchema.ServerListScreen.ClassName, GameSchema.ServerListScreen.StaticIpSelect, value); }
            catch { }
        }
    }

    public void SelectServer(int serverId)
    {
        Close();

        try
        {
            _reflector.CallMethod(
                GameSchema.ServerListScreen.ClassName,
                GameSchema.ServerListScreen.MethodSetIpSelect,
                IntPtr.Zero,
                serverId,
                true);
        }
        catch
        {
            SelectedServerId = serverId;
        }

        var addrs = ServerAddresses;
        var ports = ServerPorts;
        var names = ServerNames;
        if (names != null && serverId >= 0 && serverId < names.Length)
        {
            try
            {
                IntPtr nameStr = _reflector.CreateMonoString(names[serverId]);
                if (nameStr != IntPtr.Zero)
                {
                    _reflector.WriteStatic(GameSchema.LoginScr.ClassName, GameSchema.LoginScr.StaticServerName, nameStr);
                }
            }
            catch { }
        }

        if (addrs != null && ports != null && serverId >= 0 && serverId < addrs.Length && serverId < ports.Length)
        {
            Connect(addrs[serverId], ports[serverId]);
        }
        else
        {
            Connect();
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

    public void Connect(string host, int port)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(host) || port <= 0) return;

            IntPtr hostStr = _reflector.CreateMonoString(host);
            if (hostStr != IntPtr.Zero)
            {
                _reflector.WriteStatic(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticIp, hostStr);
            }
            _reflector.WriteStatic(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticPort, port);

            IntPtr session = _reflector.CallMethod(GameSchema.Session.ClassName, GameSchema.Session.MethodGI, IntPtr.Zero);
            if (session != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.Session.ClassName, GameSchema.Session.MethodConnect, session, host, port);
            }
            else
            {
                Connect();
            }
        }
        catch { }
    }

    public void Close()
    {
        try
        {
            IntPtr session = _reflector.CallMethod(GameSchema.Session.ClassName, GameSchema.Session.MethodGI, IntPtr.Zero);
            if (session != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.Session.ClassName, GameSchema.Session.MethodClose, session);
            }
        }
        catch { }
    }

    public bool SetClientType()
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;
            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSetClientType, service);
            return true;
        }
        catch { return false; }
    }

    public bool Login(string username, string password, string? version = null, sbyte type = 0)
    {
        try
        {
            IntPtr service = ServicePointer;
            if (service == IntPtr.Zero) return false;

            version ??= _reflector.ReadStaticString(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticVersion);
            if (string.IsNullOrEmpty(version)) version = "2.5.0";

            _reflector.CallMethod(
                GameSchema.Service.ClassName,
                GameSchema.Service.MethodLogin,
                service,
                username ?? string.Empty,
                password ?? string.Empty,
                version,
                type);

            try
            {
                _reflector.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodStartWaitDlg, IntPtr.Zero);
            }
            catch { }

            return true;
        }
        catch { return false; }
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
