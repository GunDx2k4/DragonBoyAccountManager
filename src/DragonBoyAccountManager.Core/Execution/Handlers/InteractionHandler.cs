using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class InteractionHandler(IMonoRuntime mono) :
    IGameCommandHandler<UseItemCommand>,
    IGameCommandHandler<PickItemCommand>,
    IGameCommandHandler<SendChatCommand>,
    IGameCommandHandler<WakeUpCommand>,
    IGameCommandHandler<ChangeZoneCommand>,
    IGameCommandHandler<LoginCommand>
{
    private readonly IMonoRuntime _mono = mono;

    private IntPtr GetServicePointer()
    {
        IntPtr service = _mono.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
        if (service == IntPtr.Zero)
            service = _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
        return service;
    }

    public Task<Result> HandleAsync(UseItemCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = GetServicePointer();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodUseItem, service, (sbyte)0, (sbyte)1, command.BagIndex, command.TemplateId);
        return Task.FromResult(Result.Success($"Đã dùng {command.ItemName}"));
    }

    public Task<Result> HandleAsync(PickItemCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = GetServicePointer();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodPickItem, service, command.ItemMapId);
        return Task.FromResult(Result.Success($"Đã nhặt {command.ItemName} #{command.ItemMapId}"));
    }

    public Task<Result> HandleAsync(SendChatCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
            return Task.FromResult(Result.Failure("Nội dung chat trống."));

        IntPtr service = GetServicePointer();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        string method = command.IsGlobal ? GameSchema.Service.MethodChatGlobal : GameSchema.Service.MethodChat;
        _mono.CallMethod(GameSchema.Service.ClassName, method, service, command.Text);

        return Task.FromResult(Result.Success($"Đã chat: {command.Text}"));
    }

    public Task<Result> HandleAsync(WakeUpCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = GetServicePointer();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodWakeUpFromDead, service);
        return Task.FromResult(Result.Success("Đã gửi yêu cầu hồi sinh về nhà."));
    }

    public Task<Result> HandleAsync(ChangeZoneCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = GetServicePointer();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodRequestChangeZone, service, command.ZoneId, -1);
        return Task.FromResult(Result.Success($"Đã gửi yêu cầu đổi sang khu {command.ZoneId}"));
    }

    public Task<Result> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        IntPtr service = GetServicePointer();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodLogin, service,
            command.Username, command.Password, command.Version, (sbyte)0);
        return Task.FromResult(Result.Success($"Đã gửi yêu cầu đăng nhập {command.Username}"));
    }
}
