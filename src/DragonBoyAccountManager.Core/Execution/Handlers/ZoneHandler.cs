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
            try
            {
                await Task.Delay(250, cancellationToken);
                CloseInGamePanel();
                await Task.Delay(250, cancellationToken);
                CloseInGamePanel();
            }
            catch (OperationCanceledException)
            {
            }
        }, cancellationToken);

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
            int panelOffset = _runtime.Layout.GameCanvas.Panel;
            int isShowOffset = _runtime.Layout.Panel.IsShow;
            if (gameCanvasStatic != IntPtr.Zero && panelOffset >= 0 && isShowOffset >= 0)
            {
                IntPtr panelPtr = _runtime.ReadFieldPtr(gameCanvasStatic, panelOffset);
                if (panelPtr != IntPtr.Zero)
                {
                    _runtime.WriteField(panelPtr, isShowOffset, false);
                }
            }

            IntPtr canvas = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.Instance);
            if (canvas == IntPtr.Zero)
            {
                canvas = _runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.GameCanvas);
            }
            if (canvas != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodEndDlg, canvas);
            }
        }
        catch
        {
        }
    }
}
