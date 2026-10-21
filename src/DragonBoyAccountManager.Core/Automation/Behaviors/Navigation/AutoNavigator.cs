using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Navigation;

public class AutoNavigator(ICommandDispatcher dispatcher)
{
    private readonly ICommandDispatcher _dispatcher = dispatcher;

    public async Task<Result> NavigateAsync(
        PlayerSnapshot player,
        MapSnapshot map,
        int targetX,
        int targetY,
        bool isFastTeleport = false,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!player.IsInGame || !player.IsAlive)
            return Result.Failure("Nhân vật chưa vào game hoặc đã tử vong.");

        int effectiveY = targetY <= 0 ? map.GetYGround(targetX) : targetY;

        if (Math.Abs(player.X - targetX) <= 15 && Math.Abs(player.Y - effectiveY) <= 15)
        {
            progress?.Report("Đã ở vị trí mục tiêu.");
            return Result.Success("Đã ở vị trí mục tiêu.");
        }

        if (isFastTeleport)
        {
            progress?.Report($"Dịch chuyển tức thời tới ({targetX}, {effectiveY})...");
            return await _dispatcher.DispatchAsync(new MoveCommand(targetX, effectiveY, true), cancellationToken);
        }

        if (!map.IsValid)
        {
            return await _dispatcher.DispatchAsync(new MoveCommand(targetX, effectiveY, false), cancellationToken);
        }

        progress?.Report($"Đang tìm đường A* tới ({targetX}, {effectiveY})...");
        var path = TileAStarFinder.FindPath(player.X, player.Y, targetX, effectiveY, map.Width, map.Height, map.Types);

        if (path.Count <= 1)
        {
            return await _dispatcher.DispatchAsync(new MoveCommand(targetX, effectiveY, false), cancellationToken);
        }

        progress?.Report($"Đi theo lộ trình {path.Count} mốc...");
        for (int i = 1; i < path.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return Result.Failure("Quá trình di chuyển bị hủy.");

            var (wx, wy) = path[i];
            var stepResult = await _dispatcher.DispatchAsync(new MoveCommand(wx, wy, false), cancellationToken);
            if (stepResult.IsFailure)
                return stepResult;

            await Task.Delay(100, cancellationToken);
        }

        return Result.Success($"Đã tới ({targetX}, {effectiveY})");
    }
}
