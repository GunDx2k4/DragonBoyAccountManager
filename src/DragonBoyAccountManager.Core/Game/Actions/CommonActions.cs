using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Entities;

namespace DragonBoyAccountManager.Core.Game.Actions;

public class UseSkillAction(int slotIndex) : GameAction($"Dùng skill slot {slotIndex}")
{
    public int SlotIndex => slotIndex;

    protected override Task<ActionResult> OnExecuteAsync(
        IGameContext context, 
        IProgress<string>? progress, 
        CancellationToken cancellationToken)
    {
        var result = context.Player.UseSkill(SlotIndex);
        return Task.FromResult(result.Success 
            ? ActionResult.Success(result.Message) 
            : ActionResult.Failure(result.Message));
    }
}

public class UseItemAction(sbyte bagIndex, short templateId = -1) : GameAction($"Dùng item ô {bagIndex}")
{
    protected override Task<ActionResult> OnExecuteAsync(
        IGameContext context, 
        IProgress<string>? progress, 
        CancellationToken cancellationToken)
    {
        var result = context.Player.UseItem(bagIndex, templateId);
        return Task.FromResult(result.Success 
            ? ActionResult.Success(result.Message) 
            : ActionResult.Failure(result.Message));
    }
}

public class FocusTargetAction(WorldEntity target) : GameAction($"Chọn mục tiêu {target?.ClassName} #{target?.Id}")
{
    protected override Task<ActionResult> OnExecuteAsync(
        IGameContext context, 
        IProgress<string>? progress, 
        CancellationToken cancellationToken)
    {
        if (target == null || !target.IsAlive)
            return Task.FromResult(ActionResult.Failure("Mục tiêu không hợp lệ hoặc đã biến mất."));

        bool ok = context.World.Focus(target);
        return Task.FromResult(ok ? ActionResult.Success() : ActionResult.Failure("Không thể focus mục tiêu."));
    }
}
