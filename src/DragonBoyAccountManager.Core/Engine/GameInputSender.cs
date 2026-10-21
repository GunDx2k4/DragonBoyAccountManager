namespace DragonBoyAccountManager.Core.Engine;

public static class GameInputSender
{
    public static void SendKeyDown(IMonoRuntime mono, int keyCode)
    {
        if (keyCode == 0) return;
        try
        {
            IntPtr canvas = mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticInstance);
            if (canvas != IntPtr.Zero)
            {
                mono.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodKeyPressedz, canvas, keyCode);
            }
        }
        catch
        {
        }
    }

    public static void SendKeyUp(IMonoRuntime mono, int keyCode)
    {
        if (keyCode == 0) return;
        try
        {
            IntPtr canvas = mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticInstance);
            if (canvas != IntPtr.Zero)
            {
                mono.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodKeyReleasedz, canvas, keyCode);
            }
        }
        catch
        {
        }
    }

    public static async Task SendKeyPressAsync(IMonoRuntime mono, int keyCode, int holdMs = 120)
    {
        if (keyCode == 0) return;
        SendKeyDown(mono, keyCode);
        await Task.Delay(holdMs);
        SendKeyUp(mono, keyCode);
    }

    public static void SendKeyDown(IMonoRuntime mono, GameKey key) =>
        SendKeyDown(mono, GameKeyMapper.MapToKeyCode(key));

    public static void SendKeyUp(IMonoRuntime mono, GameKey key) =>
        SendKeyUp(mono, GameKeyMapper.MapToKeyCode(key));

    public static Task SendKeyPressAsync(IMonoRuntime mono, GameKey key, int holdMs = 120) =>
        SendKeyPressAsync(mono, GameKeyMapper.MapToKeyCode(key), holdMs);
}
