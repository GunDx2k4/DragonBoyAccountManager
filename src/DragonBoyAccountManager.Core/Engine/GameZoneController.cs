namespace DragonBoyAccountManager.Core.Engine;

public static class GameZoneController
{
    public static void RequestZoneList(IMonoRuntime mono)
    {
        try
        {
            IntPtr service = mono.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
            if (service == IntPtr.Zero)
                service = mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);

            if (service != IntPtr.Zero)
            {
                mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodOpenUIZone, service);
            }
        }
        catch
        {
        }
    }

    public static void CloseZonePanel(IMonoRuntime mono)
    {
        try
        {
            IntPtr canvas = mono.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticInstance);
            if (canvas != IntPtr.Zero)
            {
                mono.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodEndDlg, canvas);
            }
        }
        catch
        {
        }
    }

    public static async Task<Result> ChangeZoneAsync(CommandDispatcher dispatcher, int zoneId, CancellationToken cancellationToken = default)
    {
        return await dispatcher.DispatchAsync(new ChangeZoneCommand(zoneId), cancellationToken);
    }
}
