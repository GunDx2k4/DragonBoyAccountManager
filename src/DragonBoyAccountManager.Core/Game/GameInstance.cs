using System.Diagnostics;
using DragonBoyAccountManager.Core.Game.Objects;
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
    public GameChat Chat { get; }
    public GameInventory Inventory { get; }
    public GameSkills Skills { get; }

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
        Chat = new GameChat(Reflector);
        Inventory = new GameInventory(Reflector);
        Skills = new GameSkills(Reflector);
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

    public bool SendChat(string text, ChatType type = ChatType.Map)
    {
        try
        {
            return Chat.SendChat(text, type);
        }
        catch
        {
            return false;
        }
    }

    public (bool Success, string Message) UseCapsule()
    {
        try
        {
            return Inventory.UseCapsule();
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi sử dụng Capsule: {ex.Message}");
        }
    }

    public (bool Success, string Message) UseTdlt()
    {
        try
        {
            return Inventory.UseTdlt();
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi sử dụng TDLT: {ex.Message}");
        }
    }

    public (bool Success, string Message) UseSkill(int slotIndex)
    {
        try
        {
            return Skills.UseSkill(slotIndex);
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi sử dụng skill: {ex.Message}");
        }
    }

    public List<GameSkillInfo> GetSkills()
    {
        try
        {
            return Skills.GetSkills();
        }
        catch
        {
            return [];
        }
    }

    public Task<bool> AutoMoveAsync(int targetX, int targetY, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var action = new DragonBoyAccountManager.Core.Game.Actions.AutoMoveAction(targetX, targetY);
        return action.ExecuteAsync(this, progress, cancellationToken);
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
