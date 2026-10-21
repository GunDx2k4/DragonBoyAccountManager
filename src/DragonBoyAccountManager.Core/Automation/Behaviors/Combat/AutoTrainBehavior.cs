using DragonBoyAccountManager.Core.Automation.Behaviors.Navigation;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed class AutoTrainOptions
{
    public IReadOnlyList<int> SelectedSkillSlots { get; init; } = [0];
    public IReadOnlyList<int> TargetMobTemplateIds { get; init; } = [];
    public IReadOnlyList<int> TargetMobIds { get; init; } = [];
    public bool IgnoreBosses { get; init; } = true;
    public bool AvoidSuperMobs { get; init; } = true;
    public bool AutoPickItems { get; init; } = true;
    public bool AutoUsePean { get; init; } = true;
    public int MinHpPercent { get; init; } = 20;
    public int MinMpPercent { get; init; } = 20;
    public bool UseTdlt { get; init; } = true;
}

public sealed class AutoTrainBehavior : AutomationBehavior<AutoTrainOptions>
{
    private readonly AutoReviveInterrupter _reviveInterrupter = new();
    private readonly AutoPeanInterrupter _peanInterrupter;
    private DateTime _lastAttackTime = DateTime.MinValue;
    private DateTime _lastTdltCheckTime = DateTime.MinValue;
    private int _skillRotationIndex;

    public AutoTrainBehavior(AutoTrainOptions options)
        : base("Auto Train", options, "AutoTrain")
    {
        _peanInterrupter = new AutoPeanInterrupter(options.AutoUsePean, options.MinHpPercent, options.MinMpPercent);
        Interrupters.Register(_reviveInterrupter);
        Interrupters.Register(_peanInterrupter);
    }

    protected override void OnOptionsChanged(AutoTrainOptions previousOptions, AutoTrainOptions newOptions)
    {
        _peanInterrupter.Enabled = newOptions.AutoUsePean;
        _peanInterrupter.MinHpPercent = newOptions.MinHpPercent;
        _peanInterrupter.MinMpPercent = newOptions.MinMpPercent;
    }

    protected override void OnStarted(IProgress<string>? progress)
    {
        progress?.Report("[Auto Train] Bắt đầu tự động luyện tập...");
    }

    protected override void OnStopped(IProgress<string>? progress)
    {
        progress?.Report("[Auto Train] Đã dừng tự động luyện tập.");
    }

    protected override async Task OnTickAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        MapSnapshot map,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!player.IsInGame || player.IsChangingMap)
        {
            await Task.Delay(300, cancellationToken);
            return;
        }

        if (Options.UseTdlt && DateTime.UtcNow - _lastTdltCheckTime > TimeSpan.FromSeconds(5))
        {
            _lastTdltCheckTime = DateTime.UtcNow;
            if (!player.IsTdltActive)
            {
                await dispatcher.DispatchAsync(new UseItemCommand(-1, GameDefinitions.DefaultTdltTemplateId, "TDLT"), cancellationToken);
            }
        }

        if (Options.AutoPickItems && world.GroundItems.Count > 0)
        {
            var myItem = world.GroundItems
                .Where(i => i.PlayerId == player.Id || i.PlayerId == -1)
                .OrderBy(i => i.DistanceTo(player.X, player.Y))
                .FirstOrDefault();

            if (myItem != null && myItem.DistanceTo(player.X, player.Y) < 150)
            {
                if (myItem.DistanceTo(player.X, player.Y) > 30)
                {
                    await dispatcher.DispatchAsync(new MoveCommand(myItem.X, myItem.Y, Options.UseTdlt), cancellationToken);
                    await Task.Delay(150, cancellationToken);
                }
                await dispatcher.DispatchAsync(new PickItemCommand(myItem.ItemMapId, myItem.Name), cancellationToken);
                return;
            }
        }

        var candidateMobs = world.Mobs.Where(m => m.IsAlive);
        if (Options.IgnoreBosses)
            candidateMobs = candidateMobs.Where(m => !m.IsBoss);
        if (Options.AvoidSuperMobs)
            candidateMobs = candidateMobs.Where(m => !m.IsSuperMob);
        if (Options.TargetMobTemplateIds.Count > 0)
            candidateMobs = candidateMobs.Where(m => Options.TargetMobTemplateIds.Contains(m.TemplateId));
        if (Options.TargetMobIds.Count > 0)
            candidateMobs = candidateMobs.Where(m => Options.TargetMobIds.Contains(m.Id));

        var targetMob = candidateMobs
            .OrderBy(m => m.DistanceTo(player.X, player.Y))
            .FirstOrDefault();

        if (targetMob == null)
        {
            await Task.Delay(200, cancellationToken);
            return;
        }

        double distance = targetMob.DistanceTo(player.X, player.Y);
        if (distance > 60)
        {
            int targetX = targetMob.X > player.X ? targetMob.X - 30 : targetMob.X + 30;
            int targetY = targetMob.IsFlying ? targetMob.Y : player.Y;
            await dispatcher.DispatchAsync(new MoveCommand(targetX, targetY, Options.UseTdlt), cancellationToken);
            await Task.Delay(100, cancellationToken);
            return;
        }

        if (DateTime.UtcNow - _lastAttackTime < TimeSpan.FromMilliseconds(400))
        {
            return;
        }

        int skillSlot = 0;
        if (Options.SelectedSkillSlots.Count > 0)
        {
            skillSlot = Options.SelectedSkillSlots[_skillRotationIndex % Options.SelectedSkillSlots.Count];
            _skillRotationIndex++;
        }

        _lastAttackTime = DateTime.UtcNow;
        var attackResult = await dispatcher.DispatchAsync(
            new AttackMobCommand(targetMob.Id, targetMob.Name, targetMob.X, skillSlot, targetMob.Pointer),
            cancellationToken);

        if (attackResult.IsSuccess)
        {
            progress?.Report($"[Auto Train] Đang đánh {targetMob.Name} #{targetMob.Id} [HP: {targetMob.Hp}/{targetMob.MaxHp}]");
        }
    }
}
