using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed class AutoTrainBehavior : AutomationBehavior<AutoTrainOptions>
{
    public const string BehaviorId = "AutoTrain";

    private const int PeanCooldownMs = 1000;
    private const int PickCooldownMs = 350;
    private const int MoveRefreshMs = 600;
    private const int MobStuckTimeoutMs = 4000;
    private const int MobBlacklistDurationMs = 5000;
    private const int MaxItemPickAttempts = 3;
    private const int ItemApproachTimeoutMs = 3000;
    private const int ItemBlacklistDurationMs = 15000;
    private const int EmptyMapZoneDelayMs = 8000;
    private const int EmptyMapNoEligibleMobsDelayMs = 2500;
    private const int ZoneChangeCooldownMs = 5000;
    private const int ZonePanelCooldownMs = 2000;
    private const int ZoneListRefreshIntervalMs = 10000;
    private const int PickRangePx = 32;
    private const int LocalPickRadiusPx = 200;
    private const int DefaultAttackRangePx = 48;
    private const int MinAttackIntervalMs = 500;
    private const int ZonePanelWaitMs = 600;
    private const int ChargeMaxDurationMs = 8000;

    private readonly Dictionary<int, DateTime> _mobBlacklist = [];
    private readonly Dictionary<short, DateTime> _itemBlacklist = [];
    private readonly Dictionary<short, long> _localSkillLastUseMs = [];
    private readonly HashSet<int> _visitedZones = [];
    private int _lastMapId = -1;
    private int _currentMobId = -1;
    private short _currentItemId = -1;
    private int _currentItemPickAttempts;
    private double _lastMobDist = double.MaxValue;
    private long _lastPlayerAttackMs;
    private DateTime _mobApproachStartTime = DateTime.MinValue;
    private DateTime _itemApproachStartTime = DateTime.MinValue;
    private DateTime _chargeStartTime = DateTime.MinValue;
    private DateTime _lastPeanTime = DateTime.MinValue;
    private DateTime _lastPickTime = DateTime.MinValue;
    private DateTime _lastMoveTime = DateTime.MinValue;
    private int _lastMoveX;
    private int _lastMoveY;
    private DateTime _mapEmptySince = DateTime.MinValue;
    private DateTime _lastZoneHopTime = DateTime.MinValue;
    private DateTime _lastZonePanelTime = DateTime.MinValue;
    private DateTime _zonePanelRequestedAt = DateTime.MinValue;
    private DateTime _zonesRefreshedAt = DateTime.MinValue;
    private bool _takeoverSynced;

    public AutoTrainBehavior(AutoTrainOptions options)
        : base("Auto Train", options, BehaviorId)
    {
    }

    protected override int TickDelayMs => 200;

    protected override void OnStarted(IProgress<string>? progress)
    {
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
            _currentMobId = -1;
            _mapEmptySince = DateTime.MinValue;
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

        var now = DateTime.UtcNow;

        await TryUsePeanAsync(context, player, now, progress, cancellationToken);

        if (await HandleChargingStateAsync(context, player, now, cancellationToken))
        {
            return;
        }

        if (await TryPickGroundItemAsync(context, player, world, now, progress, cancellationToken))
        {
            _mobApproachStartTime = DateTime.MinValue;
            return;
        }

        if (await TryCombatAsync(context, player, world, now, progress, cancellationToken))
        {
            return;
        }

        await TryChangeZoneAsync(context, world, now, progress, cancellationToken);
    }

    private void ResetState()
    {
        _mobBlacklist.Clear();
        _itemBlacklist.Clear();
        _localSkillLastUseMs.Clear();
        _visitedZones.Clear();
        _lastMapId = -1;
        _currentMobId = -1;
        _currentItemId = -1;
        _currentItemPickAttempts = 0;
        _lastMobDist = double.MaxValue;
        _lastPlayerAttackMs = 0;
        _mobApproachStartTime = DateTime.MinValue;
        _itemApproachStartTime = DateTime.MinValue;
        _chargeStartTime = DateTime.MinValue;
        _lastPeanTime = DateTime.MinValue;
        _lastPickTime = DateTime.MinValue;
        _lastMoveTime = DateTime.MinValue;
        _lastMoveX = 0;
        _lastMoveY = 0;
        _mapEmptySince = DateTime.MinValue;
        _lastZoneHopTime = DateTime.MinValue;
        _lastZonePanelTime = DateTime.MinValue;
        _zonePanelRequestedAt = DateTime.MinValue;
        _zonesRefreshedAt = DateTime.MinValue;
        _takeoverSynced = false;
    }

    private void OnMapChanged(int newMapId)
    {
        _lastMapId = newMapId;
        _currentMobId = -1;
        _currentItemId = -1;
        _currentItemPickAttempts = 0;
        _lastMobDist = double.MaxValue;
        _mobApproachStartTime = DateTime.MinValue;
        _itemApproachStartTime = DateTime.MinValue;
        _chargeStartTime = DateTime.MinValue;
        _mobBlacklist.Clear();
        _itemBlacklist.Clear();
        _visitedZones.Clear();
        _mapEmptySince = DateTime.MinValue;
        _lastMoveTime = DateTime.MinValue;
        _zonePanelRequestedAt = DateTime.MinValue;
        _zonesRefreshedAt = DateTime.MinValue;
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

    private async Task TryUsePeanAsync(
        ISessionContext context,
        PlayerSnapshot player,
        DateTime now,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!Options.AutoUsePean || player.MaxHp <= 0 || player.MaxMp <= 0)
        {
            return;
        }

        if ((now - _lastPeanTime).TotalMilliseconds < PeanCooldownMs)
        {
            return;
        }

        long hpPercent = player.Hp * 100 / player.MaxHp;
        long mpPercent = player.Mp * 100 / player.MaxMp;
        if (hpPercent >= Options.MinHpPercent && mpPercent >= Options.MinMpPercent)
        {
            return;
        }

        var pean = player.BagItemList.FirstOrDefault(i => i.IsPean && i.Quantity > 0);
        if (pean == null)
        {
            return;
        }

        _lastPeanTime = now;
        progress?.Report($"[Auto Train] Dùng {pean.Name} (HP: {hpPercent}%, MP: {mpPercent}%).");
        await context.Dispatcher.DispatchAsync(
            new UseItemCommand(pean.Index, pean.TemplateId, pean.Name),
            cancellationToken);
    }

    private async Task<bool> TryPickGroundItemAsync(
        ISessionContext context,
        PlayerSnapshot player,
        WorldSnapshot world,
        DateTime now,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!Options.AutoPick || world.GroundItems.Count == 0)
        {
            _currentItemId = -1;
            _currentItemPickAttempts = 0;
            return false;
        }

        int buaThuHutIconId = GameDefinitions.AutoTrainBuas[3].IconId;
        bool hasMagnetCharm = player.ActiveBuffIconIdList.Contains(buaThuHutIconId);
        if (hasMagnetCharm && !Options.PickAllMap)
        {
            return false;
        }

        PruneExpired(_itemBlacklist, now);
        var includeIds = ParseIdSet(Options.FilterPickIds);
        var excludeIds = ParseIdSet(Options.FilterExcludeIds);

        GroundItemSnapshot? closest = null;
        double closestDist = double.MaxValue;

        foreach (var item in world.GroundItems)
        {
            if (_itemBlacklist.ContainsKey(item.ItemMapId))
            {
                continue;
            }

            bool isAllowedOwner = Options.PickOthersItems || item.PlayerId == player.Id || item.PlayerId == -1;
            if (!isAllowedOwner)
            {
                continue;
            }

            if (excludeIds.Contains(item.TemplateId))
            {
                continue;
            }

            if (includeIds.Count > 0 && !includeIds.Contains(item.TemplateId))
            {
                continue;
            }

            double dist = item.DistanceTo(player.X, player.Y);
            if (!Options.PickAllMap && dist > LocalPickRadiusPx)
            {
                continue;
            }

            if (dist < closestDist)
            {
                closestDist = dist;
                closest = item;
            }
        }

        if (closest == null)
        {
            _currentItemId = -1;
            _currentItemPickAttempts = 0;
            return false;
        }

        if (closest.ItemMapId != _currentItemId)
        {
            _currentItemId = closest.ItemMapId;
            _currentItemPickAttempts = 0;
            _itemApproachStartTime = now;
        }

        if (closestDist > PickRangePx)
        {
            if ((now - _itemApproachStartTime).TotalMilliseconds > ItemApproachTimeoutMs)
            {
                BlacklistItem(closest.ItemMapId, now);
                return false;
            }

            bool useFastTeleport = player.IsTdltActive && Options.PickAllMap;
            await MoveTowardAsync(context, closest.X, closest.Y, useFastTeleport, now, cancellationToken);
            progress?.Report($"[Auto Train] Chạy tới nhặt {closest.Name}...");
            return true;
        }

        if ((now - _lastPickTime).TotalMilliseconds >= PickCooldownMs)
        {
            _currentItemPickAttempts++;
            if (_currentItemPickAttempts > MaxItemPickAttempts)
            {
                BlacklistItem(closest.ItemMapId, now);
                progress?.Report($"[Auto Train] Bỏ qua nhặt {closest.Name} (túi đầy hoặc kẹt).");
                return false;
            }

            _lastPickTime = now;
            await context.Dispatcher.DispatchAsync(
                new PickItemCommand(closest.ItemMapId, closest.Name),
                cancellationToken);
            progress?.Report($"[Auto Train] Nhặt {closest.Name}.");
        }

        return true;
    }

    private async Task<bool> HandleChargingStateAsync(
        ISessionContext context,
        PlayerSnapshot player,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!player.IsCharging)
        {
            _chargeStartTime = DateTime.MinValue;
            return false;
        }

        if (_chargeStartTime == DateTime.MinValue)
        {
            _chargeStartTime = now;
        }

        bool hpRecovered = player.MaxHp <= 0 || player.Hp * 100 / player.MaxHp >= 95;
        bool mpRecovered = player.MaxMp <= 0 || player.Mp * 100 / player.MaxMp >= 95;
        if ((!hpRecovered || !mpRecovered) && (now - _chargeStartTime).TotalMilliseconds < ChargeMaxDurationMs)
        {
            _mobApproachStartTime = now;
            return true;
        }

        _chargeStartTime = DateTime.MinValue;
        await context.Dispatcher.DispatchAsync(new StopSkillChargeCommand(), cancellationToken);
        return false;
    }

    private async Task<bool> TryCombatAsync(
        ISessionContext context,
        PlayerSnapshot player,
        WorldSnapshot world,
        DateTime now,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        PruneExpired(_mobBlacklist, now);

        var targetMob = SelectTargetMob(player, world);
        if (targetMob == null)
        {
            _currentMobId = -1;
            return false;
        }

        double dist = Math.Sqrt(Math.Pow(targetMob.X - player.X, 2) + Math.Pow(targetMob.Y - player.Y, 2));

        if (targetMob.Id != _currentMobId || _mobApproachStartTime == DateTime.MinValue)
        {
            _currentMobId = targetMob.Id;
            _mobApproachStartTime = now;
            _lastMobDist = dist;
        }
        else if (dist < _lastMobDist - 10)
        {
            _lastMobDist = dist;
            _mobApproachStartTime = now;
        }

        var skill = SelectReadySkill(player);
        if (skill is { IsSelfOrBuffSkill: true, TemplateId: not 6 })
        {
            _mapEmptySince = DateTime.MinValue;
            _mobApproachStartTime = now;
            if (IsSkillDispatchGateReady(skill))
            {
                await DispatchAttackMobAsync(context, targetMob, skill, cancellationToken);
            }
            return true;
        }

        int rangeX = skill is { Dx: > 0 } ? Math.Max(40, skill.Dx) : DefaultAttackRangePx;
        int rangeY = Math.Max(rangeX, skill is { Dy: > 0 } ? Math.Max(56, skill.Dy) : 56);
        int dx = Math.Abs(player.X - targetMob.X);
        int dy = Math.Abs(player.Y - targetMob.Y);

        var dest = context.CurrentMap.IsValid
            ? context.CurrentMap.ResolveSafePosition(
                targetMob.X,
                targetMob.Y,
                preferGround: targetMob.Y <= 0 || (targetMob.IsFlying && !player.IsTdltActive))
            : (X: targetMob.X, Y: targetMob.Y);

        if (targetMob.IsFlying && !player.IsTdltActive && Math.Abs(dest.Y - targetMob.Y) > rangeY)
        {
            _mobBlacklist[targetMob.Id] = now.AddMilliseconds(MobBlacklistDurationMs);
            _currentMobId = -1;
            progress?.Report($"[Auto Train] Bỏ qua {targetMob.Name} #{targetMob.Id} (quái bay ngoài tầm).");
            return false;
        }

        _mapEmptySince = DateTime.MinValue;

        bool atSafeStandPos = Math.Abs(player.X - dest.X) <= 18
            && Math.Abs(player.Y - dest.Y) <= 24
            && dx <= Math.Max(rangeX, 60)
            && Math.Abs(player.Y - targetMob.Y) <= Math.Max(rangeY, 64);

        if ((dx > rangeX || dy > rangeY) && !atSafeStandPos)
        {
            if ((now - _mobApproachStartTime).TotalMilliseconds > MobStuckTimeoutMs)
            {
                _mobBlacklist[targetMob.Id] = now.AddMilliseconds(MobBlacklistDurationMs);
                _currentMobId = -1;
                progress?.Report($"[Auto Train] Bỏ qua {targetMob.Name} #{targetMob.Id} do kẹt tiếp cận.");
                return true;
            }

            bool moved = await MoveTowardAsync(context, dest.X, dest.Y, isFastTeleport: player.IsTdltActive, now, cancellationToken);
            if (moved && skill != null && player.IsTdltActive && Math.Abs(dest.X - targetMob.X) <= rangeX && Math.Abs(dest.Y - targetMob.Y) <= rangeY && IsSkillDispatchGateReady(skill))
            {
                await DispatchAttackMobAsync(context, targetMob, skill, cancellationToken);
            }
            return true;
        }

        _mobApproachStartTime = now;
        if (skill == null || !IsSkillDispatchGateReady(skill))
        {
            return true;
        }

        await DispatchAttackMobAsync(context, targetMob, skill, cancellationToken);
        return true;
    }

    private bool IsSkillDispatchGateReady(GameSkillInfo skill)
    {
        if (skill.SkillType == 3)
        {
            return true;
        }

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return _lastPlayerAttackMs == 0 || nowMs - _lastPlayerAttackMs >= MinAttackIntervalMs;
    }

    private async Task DispatchAttackMobAsync(
        ISessionContext context,
        WorldMobInfo targetMob,
        GameSkillInfo skill,
        CancellationToken cancellationToken)
    {
        var result = await context.Dispatcher.DispatchAsync(
            new AttackMobCommand(targetMob.Id, targetMob.Name, targetMob.X, skill.Slot),
            cancellationToken);
        if (result.IsSuccess)
        {
            long ackMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _localSkillLastUseMs[skill.SkillId] = ackMs;
            if (skill.SkillType != 3)
            {
                _lastPlayerAttackMs = ackMs;
            }
        }
    }

    private async Task TryChangeZoneAsync(
        ISessionContext context,
        WorldSnapshot world,
        DateTime now,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!Options.ChangeZoneWhenNoMobs)
        {
            return;
        }

        if (!Options.CheckAllMobs && Options.TargetMobNames.Count == 0)
        {
            return;
        }

        if (_mapEmptySince == DateTime.MinValue)
        {
            _mapEmptySince = now;
            return;
        }

        int emptyDelayMs = HasAnyMatchingMobInMap(world)
            ? EmptyMapZoneDelayMs
            : EmptyMapNoEligibleMobsDelayMs;

        if ((now - _mapEmptySince).TotalMilliseconds < emptyDelayMs)
        {
            return;
        }

        bool justRefreshed = false;
        if (_zonePanelRequestedAt != DateTime.MinValue)
        {
            if ((now - _zonePanelRequestedAt).TotalMilliseconds < ZonePanelWaitMs)
            {
                return;
            }

            _zonesRefreshedAt = now;
            _zonePanelRequestedAt = DateTime.MinValue;
            justRefreshed = true;
        }

        bool needsZoneRefresh = !justRefreshed && (world.Zones.Count == 0
            || _zonesRefreshedAt == DateTime.MinValue
            || (now - _zonesRefreshedAt).TotalMilliseconds >= ZoneListRefreshIntervalMs);

        if (needsZoneRefresh)
        {
            if ((now - _lastZonePanelTime).TotalMilliseconds >= ZonePanelCooldownMs)
            {
                _lastZonePanelTime = now;
                _zonePanelRequestedAt = now;
                await context.Dispatcher.DispatchAsync(new OpenZonePanelCommand(), cancellationToken);
            }
            return;
        }

        if (world.ZoneId >= 0)
        {
            _visitedZones.Add(world.ZoneId);
        }

        var nextZone = world.Zones.Count <= 1
            ? null
            : world.Zones
                .Where(z => !z.IsFull && z.ZoneId != world.ZoneId && !_visitedZones.Contains(z.ZoneId))
                .OrderBy(z => z.NumPlayers)
                .ThenBy(z => z.ZoneId)
                .FirstOrDefault();

        if (nextZone == null)
        {
            _visitedZones.Clear();
            if (world.ZoneId >= 0)
            {
                _visitedZones.Add(world.ZoneId);
            }

            _mapEmptySince = now;
            _zonesRefreshedAt = DateTime.MinValue;
            return;
        }

        if ((now - _lastZoneHopTime).TotalMilliseconds < ZoneChangeCooldownMs)
        {
            return;
        }

        _lastZoneHopTime = now;
        _mapEmptySince = DateTime.MinValue;
        _zonesRefreshedAt = DateTime.MinValue;
        _zonePanelRequestedAt = DateTime.MinValue;
        progress?.Report($"[Auto Train] Hết quái, chuyển sang {nextZone.DisplayText}...");
        await context.Dispatcher.DispatchAsync(new ChangeZoneCommand(nextZone.ZoneId), cancellationToken);
    }

    private async Task<bool> MoveTowardAsync(
        ISessionContext context,
        int targetX,
        int targetY,
        bool isFastTeleport,
        DateTime now,
        CancellationToken cancellationToken)
    {
        bool allowFastTeleport = isFastTeleport && context.CurrentPlayer.IsTdltActive;
        bool sameTarget = Math.Abs(targetX - _lastMoveX) <= 20 && Math.Abs(targetY - _lastMoveY) <= 20;
        if (!allowFastTeleport && sameTarget && (now - _lastMoveTime).TotalMilliseconds < MoveRefreshMs)
        {
            return false;
        }

        _lastMoveX = targetX;
        _lastMoveY = targetY;
        _lastMoveTime = now;

        var result = await context.Dispatcher.DispatchAsync(
            new NavigateCommand(targetX, targetY, allowFastTeleport),
            cancellationToken);
        return result.IsSuccess;
    }

    private WorldMobInfo? SelectTargetMob(PlayerSnapshot player, WorldSnapshot world)
    {
        if (_currentMobId != -1)
        {
            var sticky = world.Mobs.FirstOrDefault(m => m.Id == _currentMobId && IsMobEligible(m));
            if (sticky != null)
            {
                return sticky;
            }
        }

        WorldMobInfo? best = null;
        int minDistance = int.MaxValue;

        foreach (var mob in world.Mobs)
        {
            if (!IsMobEligible(mob))
            {
                continue;
            }

            int dist = Math.Abs(mob.X - player.X) + Math.Abs(mob.Y - player.Y);
            if (dist < minDistance)
            {
                minDistance = dist;
                best = mob;
            }
        }

        return best;
    }

    private bool IsMobEligible(WorldMobInfo mob)
    {
        if (!mob.IsAlive || mob.Hp <= 0 || mob.Status == 0 || mob.Status == 1)
        {
            return false;
        }

        if (mob.X == 0 && mob.Y == 0)
        {
            return false;
        }

        if (_mobBlacklist.ContainsKey(mob.Id))
        {
            return false;
        }

        if (!MatchesMobBaseCriteria(mob))
        {
            return false;
        }

        if (Options.CheckAttackByHp)
        {
            if (Options.MinMobHp > 0 && mob.Hp < Options.MinMobHp)
            {
                return false;
            }

            if (Options.MaxMobHp > 0 && mob.Hp > Options.MaxMobHp)
            {
                return false;
            }
        }

        return true;
    }

    private bool HasAnyMatchingMobInMap(WorldSnapshot world)
    {
        foreach (var mob in world.Mobs)
        {
            if (!MatchesMobBaseCriteria(mob))
            {
                continue;
            }

            if (Options.CheckAttackByHp && mob.MaxHp > 0)
            {
                if (Options.MinMobHp > 0 && mob.MaxHp < Options.MinMobHp)
                {
                    continue;
                }

                if (Options.MaxMobHp > 0 && mob.MaxHp > Options.MaxMobHp)
                {
                    continue;
                }
            }

            return true;
        }

        return false;
    }

    private bool MatchesMobBaseCriteria(WorldMobInfo mob)
    {
        if (Options.AvoidSuperMobs && (mob.IsBoss || mob.IsSuperMob))
        {
            return false;
        }

        if (!Options.CheckAllMobs)
        {
            if (Options.TargetMobNames.Count == 0)
            {
                return false;
            }

            bool match = false;
            for (int i = 0; i < Options.TargetMobNames.Count; i++)
            {
                if (string.Equals(mob.Name, Options.TargetMobNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    match = true;
                    break;
                }
            }

            if (!match)
            {
                return false;
            }
        }

        return true;
    }

    private GameSkillInfo? SelectReadySkill(PlayerSnapshot player)
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var skills = player.SkillList;
        if (skills.Count == 0)
        {
            return null;
        }

        if (Options.SelectedSkillSlots.Count > 0)
        {
            foreach (int slot in Options.SelectedSkillSlots)
            {
                var skill = skills.FirstOrDefault(s => s.Slot == slot);
                if (skill != null && IsSkillEligibleForCombat(skill, player, nowMs))
                {
                    return skill;
                }
            }

            return skills.FirstOrDefault(s => s.ManaUse == 0 && s.ManaUseType == 0 && !s.IsSelfOrBuffSkill && s.TemplateId != 14 && IsSkillReadyWithLocalTracker(s, nowMs));
        }

        return skills.FirstOrDefault(s => IsSkillEligibleForCombat(s, player, nowMs))
            ?? skills.FirstOrDefault(s => s.ManaUse == 0 && s.ManaUseType == 0 && !s.IsSelfOrBuffSkill && s.TemplateId != 14 && IsSkillReadyWithLocalTracker(s, nowMs));
    }

    private bool IsSkillEligibleForCombat(GameSkillInfo skill, PlayerSnapshot player, long nowMs)
    {
        if (skill.TemplateId == 14)
        {
            return false;
        }

        if (skill.SkillType == 2 || skill.TemplateId == 7)
        {
            int hpThreshold = Options.AutoUsePean ? Options.MinHpPercent : 50;
            if (player.MaxHp <= 0 || player.Hp * 100 / player.MaxHp >= hpThreshold)
            {
                return false;
            }
        }
        else if (skill.TemplateId == 8)
        {
            if (player.IsCharging)
            {
                return false;
            }

            int hpThreshold = Options.AutoUsePean ? Options.MinHpPercent : 50;
            int mpThreshold = Options.AutoUsePean ? Options.MinMpPercent : 50;
            bool lowHp = player.MaxHp > 0 && player.Hp * 100 / player.MaxHp < hpThreshold;
            bool lowMp = player.MaxMp > 0 && player.Mp * 100 / player.MaxMp < mpThreshold;
            if (!lowHp && !lowMp)
            {
                return false;
            }
        }

        return skill.HasEnoughMana(player.Mp, player.MaxMp) && IsSkillReadyWithLocalTracker(skill, nowMs);
    }

    private bool IsSkillReadyWithLocalTracker(GameSkillInfo skill, long nowMs)
    {
        if (!skill.IsReady(nowMs))
        {
            return false;
        }

        if (_localSkillLastUseMs.TryGetValue(skill.SkillId, out long localLastUse))
        {
            return nowMs - localLastUse >= Math.Max(500, skill.CoolDown);
        }

        return true;
    }

    private void BlacklistItem(short itemMapId, DateTime now)
    {
        _itemBlacklist[itemMapId] = now.AddMilliseconds(ItemBlacklistDurationMs);
        _currentItemPickAttempts = 0;
        _currentItemId = -1;
    }

    private static void PruneExpired<TKey>(Dictionary<TKey, DateTime> blacklist, DateTime now) where TKey : notnull
    {
        if (blacklist.Count == 0)
        {
            return;
        }

        List<TKey>? expired = null;
        foreach (var pair in blacklist)
        {
            if (now >= pair.Value)
            {
                expired ??= [];
                expired.Add(pair.Key);
            }
        }

        if (expired != null)
        {
            foreach (var id in expired)
            {
                blacklist.Remove(id);
            }
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
