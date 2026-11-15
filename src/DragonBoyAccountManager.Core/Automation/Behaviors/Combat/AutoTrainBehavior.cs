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
    private const int EmptyMapZoneDelayMs = 4000;
    private const int ZoneChangeCooldownMs = 5000;
    private const int ZonePanelCooldownMs = 2000;
    private const int PickRangePx = 32;
    private const int LocalPickRadiusPx = 200;
    private const int DefaultAttackRangePx = 48;

    private readonly Dictionary<int, DateTime> _mobBlacklist = [];
    private readonly Dictionary<short, DateTime> _itemBlacklist = [];
    private readonly HashSet<int> _visitedZones = [];
    private int _lastMapId = -1;
    private int _currentMobId = -1;
    private short _currentItemId = -1;
    private int _currentItemPickAttempts;
    private double _lastMobDist = double.MaxValue;
    private DateTime _mobApproachStartTime = DateTime.MinValue;
    private DateTime _itemApproachStartTime = DateTime.MinValue;
    private DateTime _lastPeanTime = DateTime.MinValue;
    private DateTime _lastPickTime = DateTime.MinValue;
    private DateTime _lastMoveTime = DateTime.MinValue;
    private int _lastMoveX;
    private int _lastMoveY;
    private DateTime _mapEmptySince = DateTime.MinValue;
    private DateTime _lastZoneHopTime = DateTime.MinValue;
    private DateTime _lastZonePanelTime = DateTime.MinValue;
    private bool _takeoverSynced;

    public AutoTrainBehavior(AutoTrainOptions options)
        : base("Auto Train", options, BehaviorId)
    {
    }

    protected override int TickDelayMs => 200;

    protected override void OnStarted(IProgress<string>? progress)
    {
        ResetState();
        DispatchTakeoverCommand(Options, clearAuxiliary: false);
        progress?.Report("[Auto Train] Đã bật tự động luyện tập (Out-of-Process).");
    }

    protected override void OnStopped(IProgress<string>? progress)
    {
        ResetState();
        DispatchTakeoverCommand(Options, clearAuxiliary: true);
        progress?.Report("[Auto Train] Đã dừng tự động luyện tập.");
    }

    protected override void OnOptionsChanged(AutoTrainOptions previousOptions, AutoTrainOptions newOptions)
    {
        if (Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            var cmd = IsRunning
                ? BuildInGameAuxiliaryCommand(newOptions, clearAuxiliary: false, reason: "DBAM_Takeover")
                : BuildInGameAuxiliaryCommand(newOptions, clearAuxiliary: true, reason: "DBAM_SaveConfig");
            _ = DispatchSafeAsync(ctx, cmd);
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
                BuildInGameAuxiliaryCommand(Options, clearAuxiliary: false, reason: "DBAM_Takeover"),
                cancellationToken);
        }

        var now = DateTime.UtcNow;

        await TryUsePeanAsync(context, player, now, progress, cancellationToken);

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
        _visitedZones.Clear();
        _lastMapId = -1;
        _currentMobId = -1;
        _currentItemId = -1;
        _currentItemPickAttempts = 0;
        _lastMobDist = double.MaxValue;
        _mobApproachStartTime = DateTime.MinValue;
        _itemApproachStartTime = DateTime.MinValue;
        _lastPeanTime = DateTime.MinValue;
        _lastPickTime = DateTime.MinValue;
        _lastMoveTime = DateTime.MinValue;
        _lastMoveX = 0;
        _lastMoveY = 0;
        _mapEmptySince = DateTime.MinValue;
        _lastZoneHopTime = DateTime.MinValue;
        _lastZonePanelTime = DateTime.MinValue;
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
        _mobBlacklist.Clear();
        _itemBlacklist.Clear();
        _visitedZones.Clear();
        _mapEmptySince = DateTime.MinValue;
        _lastMoveTime = DateTime.MinValue;
        _takeoverSynced = false;
    }

    private void DispatchTakeoverCommand(AutoTrainOptions options, bool clearAuxiliary)
    {
        if (Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            _ = DispatchSafeAsync(ctx, BuildInGameAuxiliaryCommand(options, clearAuxiliary, reason: "DBAM_Takeover"));
        }
    }

    private static async Task DispatchSafeAsync(ISessionContext ctx, IGameCommand command)
    {
        try
        {
            await ctx.Dispatcher.DispatchAsync(command, CancellationToken.None).ConfigureAwait(false);
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

        _mapEmptySince = DateTime.MinValue;
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
            _mobApproachStartTime = now;
            await context.Dispatcher.DispatchAsync(
                new AttackMobCommand(targetMob.Id, targetMob.Name, targetMob.X, skill.Slot),
                cancellationToken);
            return true;
        }

        int rangeX = skill is { Dx: > 0 } ? Math.Max(36, skill.Dx) : DefaultAttackRangePx;
        int rangeY = skill is { Dy: > 0 } ? Math.Max(36, skill.Dy) : DefaultAttackRangePx;
        int dx = Math.Abs(player.X - targetMob.X);
        int dy = Math.Abs(player.Y - targetMob.Y);

        if (dx > rangeX || dy > rangeY)
        {
            if ((now - _mobApproachStartTime).TotalMilliseconds > MobStuckTimeoutMs)
            {
                _mobBlacklist[targetMob.Id] = now.AddMilliseconds(MobBlacklistDurationMs);
                _currentMobId = -1;
                progress?.Report($"[Auto Train] Bỏ qua {targetMob.Name} #{targetMob.Id} do kẹt tiếp cận.");
                return true;
            }

            bool moved = await MoveTowardAsync(context, targetMob.X, targetMob.Y, isFastTeleport: player.IsTdltActive, now, cancellationToken);
            if (moved && skill != null)
            {
                var dest = context.CurrentMap.IsValid
                    ? context.CurrentMap.ResolveSafePosition(targetMob.X, targetMob.Y)
                    : (X: targetMob.X, Y: targetMob.Y);
                if (Math.Abs(dest.X - targetMob.X) <= rangeX && Math.Abs(dest.Y - targetMob.Y) <= rangeY)
                {
                    await context.Dispatcher.DispatchAsync(
                        new AttackMobCommand(targetMob.Id, targetMob.Name, targetMob.X, skill.Slot),
                        cancellationToken);
                }
            }
            return true;
        }

        _mobApproachStartTime = now;
        if (skill == null)
        {
            return true;
        }

        await context.Dispatcher.DispatchAsync(
            new AttackMobCommand(targetMob.Id, targetMob.Name, targetMob.X, skill.Slot),
            cancellationToken);
        return true;
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

        if (_mapEmptySince == DateTime.MinValue)
        {
            _mapEmptySince = now;
            return;
        }

        if ((now - _mapEmptySince).TotalMilliseconds < EmptyMapZoneDelayMs)
        {
            return;
        }

        if (world.Zones.Count == 0)
        {
            if ((now - _lastZonePanelTime).TotalMilliseconds >= ZonePanelCooldownMs)
            {
                _lastZonePanelTime = now;
                await context.Dispatcher.DispatchAsync(new OpenZonePanelCommand(), cancellationToken);
            }
            return;
        }

        if (world.ZoneId >= 0)
        {
            _visitedZones.Add(world.ZoneId);
        }

        var nextZone = world.Zones
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

            if ((now - _lastZonePanelTime).TotalMilliseconds >= ZonePanelCooldownMs)
            {
                _lastZonePanelTime = now;
                await context.Dispatcher.DispatchAsync(new OpenZonePanelCommand(), cancellationToken);
            }
            return;
        }

        if ((now - _lastZoneHopTime).TotalMilliseconds < ZoneChangeCooldownMs)
        {
            return;
        }

        _lastZoneHopTime = now;
        _mapEmptySince = DateTime.MinValue;
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

        if (Options.AvoidSuperMobs && (mob.IsBoss || mob.IsSuperMob))
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

        if (!Options.CheckAllMobs && Options.TargetMobNames.Count > 0)
        {
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
                if (skill != null && skill.HasEnoughMana(player.Mp, player.MaxMp) && skill.IsReady(nowMs))
                {
                    return skill;
                }
            }

            return skills.FirstOrDefault(s => s.ManaUse == 0 && s.ManaUseType == 0 && !s.IsSelfOrBuffSkill && s.IsReady(nowMs));
        }

        return skills.FirstOrDefault(s => s.HasEnoughMana(player.Mp, player.MaxMp) && s.IsReady(nowMs))
            ?? skills.FirstOrDefault(s => s.ManaUse == 0 && s.ManaUseType == 0 && !s.IsSelfOrBuffSkill && s.IsReady(nowMs));
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
        bool clearAuxiliary,
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
            SelectedItemTemplateIds: clearAuxiliary ? [] : options.SelectedItemTemplateIds,
            SelectedBuaTemplateIds: clearAuxiliary ? [] : options.SelectedBuaTemplateIds,
            AutoPick: false,
            PickOthersItems: false,
            PickAllMap: false,
            AutoDropItems: !clearAuxiliary && options.AutoDropItems,
            FilterPickIds: string.Empty,
            FilterExcludeIds: string.Empty,
            FilterDropIds: clearAuxiliary ? string.Empty : options.FilterDropIds);
}
