using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Coordinators;

public sealed class CombatCoordinator : ICombatCoordinator
{
    private const int MoveRefreshMs = 600;
    private const int MobStuckTimeoutMs = 4000;
    private const int MobBlacklistDurationMs = 5000;
    private const int DefaultAttackRangePx = 48;
    private const int MinAttackIntervalMs = 500;

    private readonly Dictionary<int, DateTime> _mobBlacklist = [];
    private readonly List<int> _expiredMobKeys = [];
    private readonly Dictionary<short, long> _localSkillLastUseMs = [];
    private int _currentMobId = -1;
    private double _lastMobDist = double.MaxValue;
    private long _lastPlayerAttackMs;
    private DateTime _mobApproachStartTime = DateTime.MinValue;
    private DateTime _lastMoveTime = DateTime.MinValue;
    private int _lastMoveX;
    private int _lastMoveY;

    public void Reset()
    {
        _mobBlacklist.Clear();
        _expiredMobKeys.Clear();
        _localSkillLastUseMs.Clear();
        _currentMobId = -1;
        _lastMobDist = double.MaxValue;
        _lastPlayerAttackMs = 0;
        _mobApproachStartTime = DateTime.MinValue;
        _lastMoveTime = DateTime.MinValue;
        _lastMoveX = 0;
        _lastMoveY = 0;
    }

    public void BlacklistTarget(int targetId, DateTime now)
    {
        _mobBlacklist[targetId] = now.AddMilliseconds(MobBlacklistDurationMs);
        _currentMobId = -1;
        _lastMobDist = double.MaxValue;
        _mobApproachStartTime = DateTime.MinValue;
    }

    public async Task<bool> TryExecuteCombatAsync(
        ISessionContext context,
        PlayerSnapshot player,
        WorldSnapshot world,
        CombatOptions options,
        string logPrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        PruneExpiredMobBlacklist(now);

        var targetMob = SelectTargetMob(player, world, options);
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

        var skill = SelectReadySkill(player, options);
        if (skill is { IsSelfOrBuffSkill: true, TemplateId: not 6 })
        {
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
                preferGround: targetMob.Y <= 0)
            : (X: targetMob.X, Y: targetMob.Y);

        bool atSafeStandPos = Math.Abs(player.X - dest.X) <= 18
            && Math.Abs(player.Y - dest.Y) <= 24
            && dx <= Math.Max(rangeX, 60)
            && Math.Abs(player.Y - targetMob.Y) <= Math.Max(rangeY, 64);

        if ((dx > rangeX || dy > rangeY) && !atSafeStandPos)
        {
            if ((now - _mobApproachStartTime).TotalMilliseconds > MobStuckTimeoutMs)
            {
                BlacklistTarget(targetMob.Id, now);
                progress?.Report($"{logPrefix} Bỏ qua {targetMob.Name} #{targetMob.Id} do kẹt tiếp cận.");
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

    private WorldMobInfo? SelectTargetMob(PlayerSnapshot player, WorldSnapshot world, CombatOptions options)
    {
        if (_currentMobId != -1)
        {
            for (int i = 0; i < world.Mobs.Count; i++)
            {
                var m = world.Mobs[i];
                if (m.Id == _currentMobId && IsMobEligible(m, options))
                {
                    return m;
                }
            }
        }

        WorldMobInfo? best = null;
        int minDistance = int.MaxValue;

        for (int i = 0; i < world.Mobs.Count; i++)
        {
            var mob = world.Mobs[i];
            if (!IsMobEligible(mob, options))
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

    private bool IsMobEligible(WorldMobInfo mob, CombatOptions options)
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

        if (!MatchesMobBaseCriteria(mob, options))
        {
            return false;
        }

        if (options.CheckAttackByHp)
        {
            if (options.MinMobHp > 0 && mob.Hp < options.MinMobHp)
            {
                return false;
            }

            if (options.MaxMobHp > 0 && mob.Hp > options.MaxMobHp)
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesMobBaseCriteria(WorldMobInfo mob, CombatOptions options)
    {
        if (options.AvoidSuperMobs && (mob.IsBoss || mob.IsSuperMob))
        {
            return false;
        }

        if (!options.CheckAllMobs)
        {
            if (options.TargetMobNames.Count == 0)
            {
                return false;
            }

            bool match = false;
            for (int i = 0; i < options.TargetMobNames.Count; i++)
            {
                if (string.Equals(mob.Name, options.TargetMobNames[i], StringComparison.OrdinalIgnoreCase))
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

    private GameSkillInfo? SelectReadySkill(PlayerSnapshot player, CombatOptions options)
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var skills = player.SkillList;
        if (skills.Count == 0)
        {
            return null;
        }

        if (options.SelectedSkillSlots.Count > 0)
        {
            for (int p = 0; p < options.SelectedSkillSlots.Count; p++)
            {
                int slot = options.SelectedSkillSlots[p];
                for (int i = 0; i < skills.Count; i++)
                {
                    var skill = skills[i];
                    if (skill.Slot == slot && IsSkillEligibleForCombat(skill, player, options, nowMs))
                    {
                        return skill;
                    }
                }
            }

            return FindFallbackSkill(skills, nowMs);
        }

        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            if (IsSkillEligibleForCombat(skill, player, options, nowMs))
            {
                return skill;
            }
        }

        return FindFallbackSkill(skills, nowMs);
    }

    private GameSkillInfo? FindFallbackSkill(IReadOnlyList<GameSkillInfo> skills, long nowMs)
    {
        for (int i = 0; i < skills.Count; i++)
        {
            var s = skills[i];
            if (s.ManaUse == 0 && s.ManaUseType == 0 && !s.IsSelfOrBuffSkill && s.TemplateId != 14 && IsSkillReadyWithLocalTracker(s, nowMs))
            {
                return s;
            }
        }

        return null;
    }

    private bool IsSkillEligibleForCombat(GameSkillInfo skill, PlayerSnapshot player, CombatOptions options, long nowMs)
    {
        if (skill.TemplateId == 14)
        {
            return false;
        }

        if (skill.SkillType == 2 || skill.TemplateId == 7)
        {
            int hpThreshold = options.AutoUsePean ? options.MinHpPercent : 50;
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

            int hpThreshold = options.AutoUsePean ? options.MinHpPercent : 50;
            int mpThreshold = options.AutoUsePean ? options.MinMpPercent : 50;
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

    private void PruneExpiredMobBlacklist(DateTime now)
    {
        if (_mobBlacklist.Count == 0)
        {
            return;
        }

        _expiredMobKeys.Clear();
        foreach (var pair in _mobBlacklist)
        {
            if (now >= pair.Value)
            {
                _expiredMobKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < _expiredMobKeys.Count; i++)
        {
            _mobBlacklist.Remove(_expiredMobKeys[i]);
        }
    }
}
