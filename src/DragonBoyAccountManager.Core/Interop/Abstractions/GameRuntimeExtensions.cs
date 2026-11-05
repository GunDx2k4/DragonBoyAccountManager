using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Interop.Abstractions;

public static class GameRuntimeExtensions
{
    public static IntPtr GetMyCharPtr(this IGameRuntime runtime)
    {
        return runtime.ReadStatic<IntPtr>(GameSchema.Char.ClassName, runtime.Layout.Char.MyChar);
    }

    public static IntPtr GetServicePtr(this IGameRuntime runtime)
    {
        IntPtr service = runtime.ReadStatic<IntPtr>(GameSchema.Service.ClassName, runtime.Layout.Service.Instance);
        if (service == IntPtr.Zero)
        {
            service = runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
        }
        return service;
    }

    public static IntPtr GetGameScrPtr(this IGameRuntime runtime)
    {
        IntPtr gameScr = runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, runtime.Layout.GameScr.Instance);
        if (gameScr == IntPtr.Zero)
        {
            gameScr = runtime.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);
        }
        return gameScr;
    }

    public static IntPtr GetGameCanvasPtr(this IGameRuntime runtime)
    {
        return runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.Instance);
    }
}
