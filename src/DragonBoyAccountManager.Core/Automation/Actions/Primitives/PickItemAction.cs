
namespace DragonBoyAccountManager.Core.Automation.Actions.Primitives;

public class PickItemAction(int itemMapId, int targetX = 0, int targetY = 0, bool autoApproach = false, bool isFastTeleport = false)
    : GameAction($"Nhặt vật phẩm #{itemMapId}")
{
    public int ItemMapId => itemMapId;
    public int TargetX => targetX;
    public int TargetY => targetY;
    public bool AutoApproach => autoApproach;
    public bool IsFastTeleport => isFastTeleport;

    protected override async Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!context.IsRunning || !context.Player.IsInGame)
        {
            return ActionResult.Failure("Nhân vật chưa vào game.");
        }

        if (AutoApproach && (TargetX != 0 || TargetY != 0))
        {
            int curX = context.Player.X;
            int curY = context.Player.Y;
            int dist = (int)Math.Sqrt(Math.Pow(TargetX - curX, 2) + Math.Pow(TargetY - curY, 2));

            if (dist > 35)
            {
                var moveAction = new AutoMoveAction(TargetX, TargetY, IsFastTeleport);
                var moveResult = await moveAction.ExecuteAsync(context, progress, cancellationToken);
                if (!moveResult.IsSuccess && !context.Player.IsChangingMap)
                {
                    return ActionResult.Failure($"Không thể tiếp cận vật phẩm: {moveResult.Message}");
                }
            }
        }

        bool success = context.Network.PickItem(ItemMapId);
        if (success)
        {
            progress?.Report($"[PickItem] Đã gửi yêu cầu nhặt vật phẩm #{ItemMapId}");
            return ActionResult.Success($"Đã nhặt vật phẩm #{ItemMapId}");
        }

        return ActionResult.Failure($"Gửi yêu cầu nhặt vật phẩm #{ItemMapId} thất bại.");
    }
}
