using DragonBoyAccountManager.Core.Automation.Interrupters;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public enum MobTargetMode
{
    ByTemplate = 0,
    ByMobId = 1
}

public sealed record AutoTrainOptions
{
    public MobTargetMode TargetMode { get; init; } = MobTargetMode.ByTemplate;
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
    public const string BehaviorId = "AutoTrain";

    private readonly AutoReviveInterrupter _reviveInterrupter = new();
    private readonly AutoPeanInterrupter _peanInterrupter;
    private DateTime _lastTdltCheckTime = DateTime.MinValue;
    private DateTime _lastAutoSyncTime = DateTime.MinValue;

    public AutoTrainBehavior(AutoTrainOptions options)
        : base("Auto Train", options, BehaviorId)
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
        if (Context != null)
        {
            _ = Context.Dispatcher.DispatchAsync(new SetInGameAutoTrainCommand(false, "DBAM_Stop"), CancellationToken.None);
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

        if (Options.UseTdlt && DateTime.UtcNow - _lastTdltCheckTime > TimeSpan.FromSeconds(5))
        {
            _lastTdltCheckTime = DateTime.UtcNow;
            if (!player.IsTdltActive && (player.BagItems == null || player.BagItemList.Any(i => i.IsTdlt && i.Quantity > 0)))
            {
                await dispatcher.DispatchAsync(new UseItemCommand(-1, GameDefinitions.DefaultTdltTemplateId, "TDLT"), cancellationToken);
            }
        }

        if (!player.IsAutoPlay && DateTime.UtcNow - _lastAutoSyncTime > TimeSpan.FromSeconds(2))
        {
            _lastAutoSyncTime = DateTime.UtcNow;
            var result = await dispatcher.DispatchAsync(new SetInGameAutoTrainCommand(true, "DBAM_AutoTrain"), cancellationToken);
            if (result.IsSuccess)
            {
                progress?.Report("[Auto Train] Đã kích hoạt tự động đánh của game client.");
            }
        }

        await Task.Delay(500, cancellationToken);
    }
}
