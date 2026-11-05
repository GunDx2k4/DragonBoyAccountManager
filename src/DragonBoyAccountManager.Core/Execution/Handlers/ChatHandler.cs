using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class ChatHandler(IGameRuntime runtime) : IGameCommandHandler<SendChatCommand>
{
    private readonly IGameRuntime _runtime = runtime;

    public Task<Result> HandleAsync(SendChatCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Text))
            return Task.FromResult(Result.Failure("Nội dung chat trống."));

        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        string method = command.IsGlobal ? GameSchema.Service.MethodChatGlobal : GameSchema.Service.MethodChat;
        _runtime.CallMethod(GameSchema.Service.ClassName, method, service, command.Text);

        return Task.FromResult(Result.Success($"Đã chat: {command.Text}"));
    }
}
