using System.Diagnostics;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public sealed class GameInstance : IDisposable
{
    public Process Process { get; }
    public MonoContext MonoContext { get; }
    public MonoReflector Reflector { get; }

    public GameMidlet Midlet { get; }
    public GameChar MyChar { get; }
    public GameCanvas Canvas { get; }
    public GameSession Session { get; }
    public GameServerListScreen ServerScreen { get; }
    public GameLoginScreen LoginScreen { get; }
    public GameTime Time { get; }
    public GameResolution Resolution { get; }
    public GameMap Map { get; }

    public int ProcessId => Process.Id;
    public bool IsRunning => !Process.HasExited;

    public event EventHandler? Exited
    {
        add => Process.Exited += value;
        remove => Process.Exited -= value;
    }

    public GameInstance(Process process, MonoContext monoContext)
    {
        Process = process;
        Process.EnableRaisingEvents = true;
        MonoContext = monoContext;
        Reflector = new MonoReflector(monoContext);

        Midlet = new GameMidlet(Reflector);
        MyChar = new GameChar(Reflector);
        Canvas = new GameCanvas(Reflector);
        Session = new GameSession(Reflector);
        ServerScreen = new GameServerListScreen(Reflector);
        LoginScreen = new GameLoginScreen(Reflector);
        Time = new GameTime(Reflector);
        Resolution = new GameResolution(Reflector);
        Map = new GameMap(Reflector);
    }

    public bool AutoKillOnDispose { get; set; } = true;

    public bool SetResolution(int width, int height, bool fullscreen = false)
    {
        try
        {
            return Resolution.SetResolution(width, height, fullscreen);
        }
        catch
        {
            return false;
        }
    }

    public void SetTimeScale(float speed)
    {
        try
        {
            Time.SetTimeScale(speed);
        }
        catch { }
    }

    public float GetTimeScale()
    {
        try
        {
            return Time.GetTimeScale();
        }
        catch
        {
            return 1.0f;
        }
    }

    public void Kill()
    {
        if (!Process.HasExited)
        {
            try { Process.Kill(); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
    }

    public void Dispose()
    {
        Dispose(AutoKillOnDispose);
        GC.SuppressFinalize(this);
    }

    public void Dispose(bool killProcess)
    {
        if (killProcess)
        {
            Kill();
        }
        MonoContext.Dispose();
        Process.Dispose();
    }
}
