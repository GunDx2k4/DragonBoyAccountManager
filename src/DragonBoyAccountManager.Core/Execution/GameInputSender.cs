using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Interop.Native;

namespace DragonBoyAccountManager.Core.Execution;

public static class GameInputSender
{
    private const uint WmKeydown = 0x0100;
    private const uint WmKeyup = 0x0101;

    private static readonly ConcurrentDictionary<IntPtr, IntPtr> WindowHandleCache = new();

    public static void SendKeyDown(IGameRuntime runtime, int keyCode)
    {
        if (keyCode == 0) return;

        SendViaMethod(runtime, GameSchema.GameCanvas.MethodKeyPressedz, keyCode);

        IntPtr hWnd = GetWindowHandle(runtime);
        int vk = MapToVirtualKey(keyCode);
        if (hWnd != IntPtr.Zero && vk != 0)
        {
            NativeMethods.PostMessage(hWnd, WmKeydown, (IntPtr)vk, IntPtr.Zero);
        }
    }

    public static void SendKeyUp(IGameRuntime runtime, int keyCode)
    {
        if (keyCode == 0) return;

        SendViaMethod(runtime, GameSchema.GameCanvas.MethodKeyReleasedz, keyCode);

        IntPtr hWnd = GetWindowHandle(runtime);
        int vk = MapToVirtualKey(keyCode);
        if (hWnd != IntPtr.Zero && vk != 0)
        {
            NativeMethods.PostMessage(hWnd, WmKeyup, (IntPtr)vk, IntPtr.Zero);
        }
    }

    public static async Task SendKeyPressAsync(IGameRuntime runtime, int keyCode, int holdMs = 120, CancellationToken cancellationToken = default)
    {
        if (keyCode == 0) return;
        SendKeyDown(runtime, keyCode);
        try
        {
            await Task.Delay(holdMs, cancellationToken);
        }
        finally
        {
            SendKeyUp(runtime, keyCode);
        }
    }

    public static void SendKeyDown(IGameRuntime runtime, GameKey key) =>
        SendKeyDown(runtime, GameKeyMapper.MapToKeyCode(key));

    public static void SendKeyUp(IGameRuntime runtime, GameKey key) =>
        SendKeyUp(runtime, GameKeyMapper.MapToKeyCode(key));

    public static Task SendKeyPressAsync(IGameRuntime runtime, GameKey key, int holdMs = 120, CancellationToken cancellationToken = default) =>
        SendKeyPressAsync(runtime, GameKeyMapper.MapToKeyCode(key), holdMs, cancellationToken);

    private static IntPtr GetWindowHandle(IGameRuntime runtime)
    {
        if (runtime.ProcessHandle == IntPtr.Zero)
            return IntPtr.Zero;

        if (WindowHandleCache.TryGetValue(runtime.ProcessHandle, out var cachedHwnd))
        {
            if (NativeMethods.IsWindow(cachedHwnd))
                return cachedHwnd;

            WindowHandleCache.TryRemove(runtime.ProcessHandle, out _);
        }

        int pid = NativeMethods.GetProcessId(runtime.ProcessHandle);
        if (pid <= 0)
            return IntPtr.Zero;

        IntPtr found = ProcessHelper.FindMainWindowHandle(pid);
        if (found != IntPtr.Zero)
        {
            WindowHandleCache[runtime.ProcessHandle] = found;
        }
        return found;
    }

    private static void SendViaMethod(IGameRuntime runtime, string methodName, int keyCode)
    {
        try
        {
            IntPtr canvas = runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.Instance);
            if (canvas != IntPtr.Zero)
            {
                runtime.CallMethod(GameSchema.GameCanvas.ClassName, methodName, canvas, keyCode);
            }
        }
        catch
        {
        }
    }

    private static int MapToVirtualKey(int keyCode) => keyCode switch
    {
        -1 => 0x26,
        -2 => 0x28,
        -3 => 0x25,
        -4 => 0x27,
        -5 => 0x0D,
        -6 => 0x70,
        -7 => 0x71,
        -8 => 0x1B,
        -21 => 0x50,
        -22 => 0x1B,
        -26 => 0x09,
        32 => 0x20,
        >= '0' and <= '9' => keyCode,
        >= 'a' and <= 'z' => keyCode - 'a' + 'A',
        >= 'A' and <= 'Z' => keyCode,
        _ => 0
    };
}
