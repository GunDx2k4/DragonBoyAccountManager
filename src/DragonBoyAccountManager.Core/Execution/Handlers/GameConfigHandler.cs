using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions.Controllers;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class GameConfigHandler(GameConfigController controller) : IGameCommandHandler<SetGameConfigCommand>
{
    private readonly GameConfigController _controller = controller ?? throw new ArgumentNullException(nameof(controller));

    public Task<Result> HandleAsync(SetGameConfigCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Config == null)
        {
            return Task.FromResult(Result.Failure("Cấu hình game không hợp lệ."));
        }

        _controller.ApplyConfig(command.Config);
        return Task.FromResult(Result.Success("Đã áp dụng cấu hình game vào bộ nhớ."));
    }
}
