using DragonBoyAccountManager.Core.Automation.Coordinators;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed class AutoTrainBehavior : AutomationBehavior<AutoTrainOptions>
{
    public const string BehaviorId = "AutoTrain";

    private readonly IGroundLootCoordinator _lootCoordinator;
    private readonly IRecoveryCoordinator _recoveryCoordinator;
    private readonly ICombatCoordinator _combatCoordinator;
    private readonly IZoneCoordinator _zoneCoordinator;
    private HashSet<short> _cachedIncludeIds = [];
    private HashSet<short> _cachedExcludeIds = [];
    private int _lastMapId = -1;
    private bool _takeoverSynced;

    public AutoTrainBehavior(
        AutoTrainOptions options,
        IGroundLootCoordinator? lootCoordinator = null,
        IRecoveryCoordinator? recoveryCoordinator = null,
        ICombatCoordinator? combatCoordinator = null,
        IZoneCoordinator? zoneCoordinator = null)
        : base("Auto Train", options, BehaviorId)
    {
        _lootCoordinator = lootCoordinator ?? new GroundLootCoordinator();
        _recoveryCoordinator = recoveryCoordinator ?? new RecoveryCoordinator();
        _combatCoordinator = combatCoordinator ?? new CombatCoordinator();
        _zoneCoordinator = zoneCoordinator ?? new ZoneCoordinator();
    }

    protected override int TickDelayMs => 200;

    protected override void OnStarted(IProgress<string>? progress)
    {
        _cachedIncludeIds = ParseIdSet(Options.FilterPickIds);
        _cachedExcludeIds = ParseIdSet(Options.FilterExcludeIds);
        ResetState();
        progress?.Report("[Auto Train] Đã bật tự động luyện tập (Out-of-Process).");
    }

    protected override async Task OnStoppedAsync(IProgress<string>? progress)
    {
        ResetState();
        if (!IsHostCancellationRequested && Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            await DispatchSafeAsync(
                ctx,
                new StopSkillChargeCommand(),
                HostCancellationToken).ConfigureAwait(false);
            await DispatchSafeAsync(
                ctx,
                BuildInGameAuxiliaryCommand(Options, keepInGamePick: true, reason: "DBAM_Takeover"),
                HostCancellationToken).ConfigureAwait(false);
        }
        progress?.Report("[Auto Train] Đã dừng tự động luyện tập.");
    }

    protected override void OnOptionsChanged(AutoTrainOptions previousOptions, AutoTrainOptions newOptions)
    {
        _cachedIncludeIds = ParseIdSet(newOptions.FilterPickIds);
        _cachedExcludeIds = ParseIdSet(newOptions.FilterExcludeIds);
        if (IsRunning && !IsStopping && !IsHostCancellationRequested && Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            var cmd = BuildInGameAuxiliaryCommand(newOptions, keepInGamePick: false, reason: "DBAM_Takeover");
            _ = DispatchSafeAsync(ctx, cmd, HostCancellationToken);
        }
    }

    protected override async Task OnTickAsync(
        ISessionContext context,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        context.Tick();

        var player = context.CurrentPlayer;
        if (!player.IsInGame || player.IsChangingMap || !player.IsAlive)
        {
            return;
        }

        var world = context.CurrentWorld;
        if (world.MapId != _lastMapId)
        {
            OnMapChanged(world.MapId);
        }

        if (!_takeoverSynced || player.IsAutoPlay)
        {
            _takeoverSynced = true;
            await context.Dispatcher.DispatchAsync(
                BuildInGameAuxiliaryCommand(Options, keepInGamePick: false, reason: "DBAM_Takeover"),
                cancellationToken);
        }

        var recoveryOptions = new RecoveryOptions(
            Options.AutoUsePean,
            Options.MinHpPercent,
            Options.MinMpPercent);

        await _recoveryCoordinator.TryUsePeanAsync(
            context,
            player,
            recoveryOptions,
            "[Auto Train]",
            progress,
            cancellationToken);

        if (await _recoveryCoordinator.HandleChargingStateAsync(context, player, cancellationToken))
        {
            return;
        }

        var lootOptions = new LootFilterOptions(
            AutoPick: Options.AutoPick,
            PickOthersItems: Options.PickOthersItems,
            PickAllMap: Options.PickAllMap,
            IsFastTeleport: player.IsTdltActive && Options.PickAllMap,
            IncludeIds: _cachedIncludeIds,
            ExcludeIds: _cachedExcludeIds);

        if (await _lootCoordinator.TryPickGroundItemsAsync(
            context,
            player,
            world,
            lootOptions,
            "[Auto Train]",
            progress,
            cancellationToken))
        {
            return;
        }

        var combatOptions = new CombatOptions(
            AvoidSuperMobs: Options.AvoidSuperMobs,
            CheckAllMobs: Options.CheckAllMobs,
            TargetMobNames: Options.TargetMobNames,
            CheckAttackByHp: Options.CheckAttackByHp,
            MinMobHp: Options.MinMobHp,
            MaxMobHp: Options.MaxMobHp,
            SelectedSkillSlots: Options.SelectedSkillSlots,
            AutoUsePean: Options.AutoUsePean,
            MinHpPercent: Options.MinHpPercent,
            MinMpPercent: Options.MinMpPercent);

        if (await _combatCoordinator.TryExecuteCombatAsync(
            context,
            player,
            world,
            combatOptions,
            "[Auto Train]",
            progress,
            cancellationToken))
        {
            return;
        }

        var zoneOptions = new ZoneHopOptions(
            ChangeZoneWhenNoMobs: Options.ChangeZoneWhenNoMobs,
            CheckAllMobs: Options.CheckAllMobs,
            TargetMobNames: Options.TargetMobNames,
            CheckAttackByHp: Options.CheckAttackByHp,
            MinMobHp: Options.MinMobHp,
            MaxMobHp: Options.MaxMobHp);

        await _zoneCoordinator.TryHopZoneAsync(
            context,
            world,
            zoneOptions,
            "[Auto Train]",
            progress,
            cancellationToken);
    }

    private void ResetState()
    {
        _lootCoordinator.Reset();
        _recoveryCoordinator.Reset();
        _combatCoordinator.Reset();
        _zoneCoordinator.Reset();
        _lastMapId = -1;
        _takeoverSynced = false;
    }

    private void OnMapChanged(int newMapId)
    {
        _lastMapId = newMapId;
        _lootCoordinator.Reset();
        _recoveryCoordinator.Reset();
        _combatCoordinator.Reset();
        _zoneCoordinator.OnMapChanged(newMapId);
        _takeoverSynced = false;
    }

    private static async Task DispatchSafeAsync(ISessionContext ctx, IGameCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await ctx.Dispatcher.DispatchAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static HashSet<short> ParseIdSet(string rawIds)
    {
        var result = new HashSet<short>();
        if (string.IsNullOrWhiteSpace(rawIds))
        {
            return result;
        }

        var tokens = rawIds.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in tokens)
        {
            if (short.TryParse(token, out short id))
            {
                result.Add(id);
            }
        }

        return result;
    }

    private static SetInGameAutoTrainCommand BuildInGameAuxiliaryCommand(
        AutoTrainOptions options,
        bool keepInGamePick,
        string reason) =>
        new(
            Enable: false,
            Reason: reason,
            AvoidSuperMobs: options.AvoidSuperMobs,
            ChangeZoneWhenNoMobs: false,
            CheckAttackByHp: options.CheckAttackByHp,
            MinMobHp: options.MinMobHp,
            MaxMobHp: options.MaxMobHp,
            AutoUsePean: false,
            PeanHpPercent: options.MinHpPercent,
            PeanMpPercent: options.MinMpPercent,
            CheckAllMobs: options.CheckAllMobs,
            TargetMobNames: options.TargetMobNames,
            SelectedSkillSlots: options.SelectedSkillSlots,
            SelectedItemTemplateIds: options.SelectedItemTemplateIds,
            SelectedBuaTemplateIds: options.SelectedBuaTemplateIds,
            AutoPick: keepInGamePick && options.AutoPick,
            PickOthersItems: keepInGamePick && options.PickOthersItems,
            PickAllMap: keepInGamePick && options.PickAllMap,
            AutoDropItems: options.AutoDropItems,
            FilterPickIds: keepInGamePick ? options.FilterPickIds : string.Empty,
            FilterExcludeIds: keepInGamePick ? options.FilterExcludeIds : string.Empty,
            FilterDropIds: options.FilterDropIds);
}
