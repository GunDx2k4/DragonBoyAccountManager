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
        return Task.FromResult(Result.Success("Đã gửi yêu cầu mở bảng chọn khu vực."));
    }

    public Task<Result> HandleAsync(CloseZonePanelCommand command, CancellationToken cancellationToken)
    {
        IntPtr canvas = _runtime.GetGameCanvasPtr();
        if (canvas == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy GameCanvas."));

        _runtime.CallMethod(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.MethodEndDlg, canvas);
        return Task.FromResult(Result.Success("Đã đóng bảng chọn khu vực."));
    }
}
