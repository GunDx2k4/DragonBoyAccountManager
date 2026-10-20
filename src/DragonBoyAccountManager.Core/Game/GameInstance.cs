using System.Diagnostics;
using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Modules;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public sealed class GameInstance : IDisposable
{
    public Process Process { get; }
    public MonoContext MonoContext { get; }
    public MonoReflector Reflector { get; }

    public IGameContext Context { get; }

    public IPlayerModule Player => Context.Player;
    public IWorldModule World => Context.World;
    public IUIModule UI => Context.UI;
    public INetworkModule Network => Context.Network;
    public IEngineModule Engine => Context.Engine;

    public int ProcessId => Process.Id;
    public bool IsRunning => !Process.HasExited;
    public bool IsQuitRequested => Engine.IsQuitRequested;

    public event EventHandler? Exited
    {
        add => Process.Exited += value;
        remove => Process.Exited -= value;
    }

    public GameInstance(Process process, MonoContext monoContext, Guid contextId = default)
    {
        Process = process;
        Process.EnableRaisingEvents = true;
        MonoContext = monoContext;
        Reflector = new MonoReflector(monoContext);

        Context = new GameContext(Reflector, () => IsRunning && !IsQuitRequested, contextId);
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
        MonoContext.Dispose();
    }
}
