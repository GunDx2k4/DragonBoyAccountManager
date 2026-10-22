using DragonBoyAccountManager.Core.Interop.Mono.Schema;

namespace DragonBoyAccountManager.Core.Interop.Mono;

public static class MonoGameExtensions
{
    public static IntPtr GetMyCharPtr(this IMonoRuntime mono)
    {
        return mono.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
    }

    public static IntPtr GetServicePtr(this IMonoRuntime mono)
    {
        IntPtr service = mono.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
        if (service == IntPtr.Zero)
        {
            service = mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
        }
        return service;
    }

    public static IntPtr GetGameScrPtr(this IMonoRuntime mono)
    {
        IntPtr gameScr = mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
        if (gameScr == IntPtr.Zero)
        {
            gameScr = mono.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);
        }
        return gameScr;
    }

    public static IntPtr GetGameCanvasPtr(this IMonoRuntime mono)
    {
        return mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticInstance);
    }
}
