using DragonBoyAccountManager.Core.Game.Context;
using DragonBoyAccountManager.Core.Game.Entities;

namespace DragonBoyAccountManager.Core.Game.Actions;

public class AttackMobAction(MobEntity targetMob, int skillSlot = 0)
    : GameAction($"Tấn công {targetMob?.Name ?? "quái"} #{targetMob?.Id ?? 0}")
{
    public MobEntity TargetMob { get; } = targetMob;
    public int SkillSlot { get; } = skillSlot;

    protected override Task<ActionResult> OnExecuteAsync(
        IGameContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!context.IsRunning || !context.Player.IsInGame || !context.Player.IsAlive)
        {
            return Task.FromResult(ActionResult.Failure("Nhân vật chưa vào game hoặc đã tử vong."));
        }

        if (TargetMob == null || TargetMob.Pointer == IntPtr.Zero || !TargetMob.IsAlive)
        {
            return Task.FromResult(ActionResult.Failure("Mục tiêu quái không hợp lệ hoặc đã chết."));
        }

        IntPtr myCharPtr = context.Player.Pointer;
        if (myCharPtr == IntPtr.Zero)
        {
            return Task.FromResult(ActionResult.Failure("Con trỏ nhân vật không hợp lệ."));
        }

        int playerX = context.Player.X;
        int mobX = TargetMob.X;
        int cdir = mobX >= playerX ? 1 : -1;
        context.Player.SetDirection(cdir);

        context.Player.SelectSkill(SkillSlot);

        bool attacked = context.Network.SendPlayerAttack(TargetMob.Pointer);
        if (!attacked)
        {
            return Task.FromResult(ActionResult.Failure($"Gửi đòn đánh thất bại tới {TargetMob.Name} #{TargetMob.Id}"));
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        context.Player.SetSkillLastTimeUsed(nowMs);

        progress?.Report($"[Attack] Đang đánh {TargetMob.Name} #{TargetMob.Id} [HP: {TargetMob.Hp}/{TargetMob.MaxHp}]");
        return Task.FromResult(ActionResult.Success($"Đã đánh {TargetMob.Name} #{TargetMob.Id}"));
    }
}
