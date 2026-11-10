using DragonBoyAccountManager.Core.Automation.Interrupters;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed class AutoTrainBehavior : AutomationBehavior<AutoTrainOptions>
{
    public const string BehaviorId = "AutoTrain";

    private readonly AutoReviveInterrupter _reviveInterrupter = new();

    public AutoTrainBehavior(AutoTrainOptions options)
        : base("Auto Train", options, BehaviorId)
    {
        Interrupters.Register(_reviveInterrupter);
    }

    protected override int TickDelayMs => 1000;

    protected override void OnStarted(IProgress<string>? progress)
    {
        if (Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            _ = ctx.Dispatcher.DispatchAsync(
                BuildInGameCommand(Options, enable: true, reason: "DBAM_Start"),
                CancellationToken.None);
        }
        progress?.Report("[Auto Train] Đã bật tự động luyện tập trong game.");
    }

    protected override void OnStopped(IProgress<string>? progress)
    {
        if (Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            _ = ctx.Dispatcher.DispatchAsync(
                BuildInGameCommand(Options, enable: false, reason: "DBAM_Stop"),
                CancellationToken.None);
        }
        progress?.Report("[Auto Train] Đã dừng tự động luyện tập trong game.");
    }

    protected override void OnOptionsChanged(AutoTrainOptions previousOptions, AutoTrainOptions newOptions)
    {
        if (Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            string reason = IsRunning ? "DBAM_Update" : "DBAM_SaveConfig";
            bool enable = IsRunning;
            var cmd = BuildInGameCommand(newOptions, enable, reason);
            _ = ctx.Dispatcher.DispatchAsync(cmd, CancellationToken.None);
        }
    }

    protected override async Task OnTickAsync(
        ISessionContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var player = context.CurrentPlayer;
        if (!player.IsInGame || player.IsChangingMap || !player.IsAlive)
        {
            return;
        }

        if (!player.IsAutoPlay)
        {
            await context.Dispatcher.DispatchAsync(
                BuildInGameCommand(Options, enable: true, reason: "DBAM_EnsureActive"),
                cancellationToken);
        }
    }

    private static SetInGameAutoTrainCommand BuildInGameCommand(AutoTrainOptions options, bool enable, string reason) =>
        new(
            Enable: enable,
            Reason: reason,
            AvoidSuperMobs: options.AvoidSuperMobs,
            ChangeZoneWhenNoMobs: options.ChangeZoneWhenNoMobs,
            CheckAttackByHp: options.CheckAttackByHp,
            MinMobHp: options.MinMobHp,
            MaxMobHp: options.MaxMobHp,
            AutoUsePean: options.AutoUsePean,
            PeanHpPercent: options.MinHpPercent,
            PeanMpPercent: options.MinMpPercent,
            CheckAllMobs: options.CheckAllMobs,
            TargetMobNames: options.TargetMobNames,
            SelectedSkillSlots: options.SelectedSkillSlots,
            SelectedItemTemplateIds: options.SelectedItemTemplateIds,
            SelectedBuaTemplateIds: options.SelectedBuaTemplateIds,
            AutoPick: options.AutoPick,
            PickOthersItems: options.PickOthersItems,
            PickAllMap: options.PickAllMap,
            AutoDropItems: options.AutoDropItems,
            FilterPickIds: options.FilterPickIds,
            FilterExcludeIds: options.FilterExcludeIds,
            FilterDropIds: options.FilterDropIds);
}
