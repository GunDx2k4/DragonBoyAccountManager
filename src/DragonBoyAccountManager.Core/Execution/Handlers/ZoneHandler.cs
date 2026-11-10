using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class ZoneHandler(IGameRuntime runtime) :
    IGameCommandHandler<ChangeZoneCommand>,
    IGameCommandHandler<OpenZonePanelCommand>,
    IGameCommandHandler<CloseZonePanelCommand>
{
    private readonly IGameRuntime _runtime = runtime;

    public Task<Result> HandleAsync(ChangeZoneCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodRequestChangeZone, service, command.ZoneId, -1);
        return Task.FromResult(Result.Success($"Đã gửi yêu cầu đổi sang khu {command.ZoneId}"));
    }

    public Task<Result> HandleAsync(OpenZonePanelCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodOpenUIZone, service);

        _ = Task.Run(async () =>
        {
            await Task.Delay(250, CancellationToken.None);
            CloseInGamePanel();
            await Task.Delay(250, CancellationToken.None);
            CloseInGamePanel();
        });

        return Task.FromResult(Result.Success("Đã gửi yêu cầu mở bảng chọn khu vực."));
    }

    public Task<Result> HandleAsync(CloseZonePanelCommand command, CancellationToken cancellationToken)
    {
        CloseInGamePanel();
        return Task.FromResult(Result.Success("Đã đóng bảng chọn khu vực."));
    }

    private void CloseInGamePanel()
    {
        try
        {
            IntPtr gameCanvasStatic = _runtime.GetStaticFieldsPtr(GameSchema.GameCanvas.ClassName);
            if (gameCanvasStatic != IntPtr.Zero)
            {
                int panelOffset = _runtime.Layout.GameCanvas.Panel >= 0 ? _runtime.Layout.GameCanvas.Panel : 208;
                IntPtr panelPtr = _runtime.ReadFieldPtr(gameCanvasStatic, panelOffset);
                if (panelPtr != IntPtr.Zero)
                {
                    int isShowOffset = _runtime.Layout.Panel.IsShow >= 0 ? _runtime.Layout.Panel.IsShow : 16;
                    _runtime.WriteField<bool>(panelPtr, isShowOffset, false);
                }
            }

            _runtime.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodEndDlg, IntPtr.Zero);
        }
        catch
        {
        }
    }
}
