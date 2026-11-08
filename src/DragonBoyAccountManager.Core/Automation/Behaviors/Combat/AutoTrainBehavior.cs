using DragonBoyAccountManager.Core.Automation.Interrupters;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed class AutoTrainBehavior : AutomationBehavior<AutoTrainOptions>
{
    public const string BehaviorId = "AutoTrain";

    private readonly AutoReviveInterrupter _reviveInterrupter = new();
    private readonly AutoPeanInterrupter _peanInterrupter;
    private DateTime _lastAutoSyncTime = DateTime.MinValue;

    public AutoTrainBehavior(AutoTrainOptions options)
        : base("Auto Train", options, BehaviorId)
    {
        _peanInterrupter = new AutoPeanInterrupter(options.AutoUsePean, options.MinHpPercent, options.MinMpPercent);
        Interrupters.Register(_reviveInterrupter);
        Interrupters.Register(_peanInterrupter);
    }

    private SetInGameAutoTrainCommand BuildCommand(bool enable)
    {
        return new SetInGameAutoTrainCommand(
            Enable: enable,
            Reason: "DBAM_AutoTrain",
            AvoidSuperMobs: Options.AvoidSuperMobs,
            ChangeZoneWhenNoMobs: Options.ChangeZoneWhenNoMobs,
            CheckAttackByHp: Options.CheckAttackByHp,
            MinMobHp: Options.MinMobHp,
            MaxMobHp: Options.MaxMobHp,
            AutoUsePean: Options.AutoUsePean,
            PeanHpPercent: Options.MinHpPercent,
            PeanMpPercent: Options.MinMpPercent,
            CheckAllMobs: Options.CheckAllMobs,
            TargetMobNames: Options.TargetMobNames,
            SelectedSkillSlots: Options.SelectedSkillSlots
        );
    }

    protected override void OnOptionsChanged(AutoTrainOptions previousOptions, AutoTrainOptions newOptions)
    {
        _peanInterrupter.Enabled = newOptions.AutoUsePean;
        _peanInterrupter.MinHpPercent = newOptions.MinHpPercent;
        _peanInterrupter.MinMpPercent = newOptions.MinMpPercent;

        if (IsRunning && Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            var cmd = BuildCommand(true);
            _ = Task.Run(() => ctx.Dispatcher.DispatchAsync(cmd, CancellationToken.None));
        }
    }

    protected override void OnStarted(IProgress<string>? progress)
    {
        _lastAutoSyncTime = DateTime.MinValue;
        progress?.Report("[Auto Train] Bắt đầu tự động luyện tập...");
    }

    protected override void OnStopped(IProgress<string>? progress)
    {
        if (Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            _ = Task.Run(() => ctx.Dispatcher.DispatchAsync(new SetInGameAutoTrainCommand(false, "DBAM_Stop"), CancellationToken.None));
        }
        progress?.Report("[Auto Train] Đã dừng tự động luyện tập.");
    }

    protected override async Task OnTickAsync(
        ISessionContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var player = context.CurrentPlayer;
        var dispatcher = context.Dispatcher;

        if (!player.IsInGame || player.IsChangingMap)
        {
            await Task.Delay(300, cancellationToken);
            return;
        }

        if ((!player.IsAutoPlay || _lastAutoSyncTime == DateTime.MinValue) && DateTime.UtcNow - _lastAutoSyncTime > TimeSpan.FromSeconds(2))
        {
            _lastAutoSyncTime = DateTime.UtcNow;
            var result = await dispatcher.DispatchAsync(BuildCommand(true), cancellationToken);
            if (result.IsSuccess)
            {
                progress?.Report("[Auto Train] Đã kích hoạt tự động đánh của game client.");
            }
        }

        await Task.Delay(500, cancellationToken);
    }
}
