using DragonBoyAccountManager.Core.Automation.Interrupters;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed class AutoTrainBehavior : AutomationBehavior<AutoTrainOptions>
{
    public const string BehaviorId = "AutoTrain";

    private readonly AutoReviveInterrupter _reviveInterrupter = new();
    private readonly AutoPeanInterrupter _peanInterrupter;
    private readonly Dictionary<int, long> _localSkillLastUseMs = new();
    private bool _inGameAutoDisabled;
    private int _waitingSpawnMobId = -1;
    private DateTime _lastZoneChangeTime = DateTime.MinValue;
    private DateTime _lastZoneOpenRequestTime = DateTime.MinValue;

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
        _inGameAutoDisabled = false;
        _waitingSpawnMobId = -1;
        _localSkillLastUseMs.Clear();
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
        var world = context.CurrentWorld;
        var dispatcher = context.Dispatcher;

        if (!player.IsInGame || player.IsChangingMap || !player.IsAlive)
        {
            await Task.Delay(250, cancellationToken);
            return;
        }

        if (player.IsAutoPlay || !_inGameAutoDisabled)
        {
            _inGameAutoDisabled = true;
            await dispatcher.DispatchAsync(new SetInGameAutoTrainCommand(false, "DBAM_Takeover"), cancellationToken);
        }

        var targetMobs = world.Mobs.Where(IsTargetMob).ToList();
        var aliveMobs = targetMobs.Where(m => m.IsAlive && (m.X > 0 || m.Y > 0)).ToList();
        var deadMobs = targetMobs.Where(m => !m.IsAlive && (m.SpawnX > 0 || m.SpawnY > 0)).ToList();

        if (aliveMobs.Count > 0)
        {
            _waitingSpawnMobId = -1;
            var targetMob = aliveMobs
                .OrderBy(m => m.DistanceTo(player.X, player.Y))
                .First();

            double dist = targetMob.DistanceTo(player.X, player.Y);
            if (player.IsTdltActive)
            {
                if (dist > 45)
                {
                    await dispatcher.DispatchAsync(new MoveCommand(targetMob.X, targetMob.Y, IsFastTeleport: true), cancellationToken);
                    await Task.Delay(50, cancellationToken);
                }
            }
            else if (dist > 55)
            {
                await dispatcher.DispatchAsync(new MoveCommand(targetMob.X, targetMob.Y, IsFastTeleport: false), cancellationToken);
                await Task.Delay(250, cancellationToken);
                return;
            }

            int skillSlot = SelectReadySkillSlot(player);
            if (skillSlot >= 0)
            {
                _localSkillLastUseMs[skillSlot] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                await dispatcher.DispatchAsync(new AttackMobCommand(targetMob.Id, targetMob.Name, targetMob.X, skillSlot), cancellationToken);
            }

            await Task.Delay(140, cancellationToken);
            return;
        }

        if (Options.ChangeZoneWhenNoMobs && await TryChangeZoneAsync(world, dispatcher, progress, cancellationToken))
        {
            await Task.Delay(500, cancellationToken);
            return;
        }

        if (player.IsTdltActive && deadMobs.Count > 0)
        {
            var nextSpawnMob = deadMobs
                .OrderBy(m => m.DeathTimestampMs)
                .ThenBy(m => player.DistanceTo(m.SpawnX, m.SpawnY))
                .First();

            double distToSpawn = player.DistanceTo(nextSpawnMob.SpawnX, nextSpawnMob.SpawnY);
            if (distToSpawn > 20)
            {
                await dispatcher.DispatchAsync(new MoveCommand(nextSpawnMob.SpawnX, nextSpawnMob.SpawnY, IsFastTeleport: true), cancellationToken);
                if (_waitingSpawnMobId != nextSpawnMob.Id)
                {
                    _waitingSpawnMobId = nextSpawnMob.Id;
                    progress?.Report($"[Auto Train] Đón đầu {nextSpawnMob.Name} #{nextSpawnMob.Id} hồi sinh tại ({nextSpawnMob.SpawnX}, {nextSpawnMob.SpawnY}).");
                }
            }
        }

        await Task.Delay(120, cancellationToken);
    }

    private bool IsTargetMob(WorldMobInfo mob)
    {
        if (Options.AvoidSuperMobs && (mob.IsSuperMob || mob.IsBoss))
            return false;

        if (!Options.CheckAllMobs && Options.TargetMobNames.Count > 0 &&
            !Options.TargetMobNames.Any(n => string.Equals(n, mob.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (Options.CheckAttackByHp)
        {
            long hpToCheck = mob.IsAlive ? mob.Hp : mob.MaxHp;
            if (hpToCheck <= 0 || hpToCheck < Options.MinMobHp || hpToCheck > Options.MaxMobHp)
                return false;
        }

        return true;
    }

    private int SelectReadySkillSlot(PlayerSnapshot player)
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var configuredSlots = Options.SelectedSkillSlots is { Count: > 0 }
            ? Options.SelectedSkillSlots
            : [0];

        if (player.SkillList.Count == 0)
        {
            int fallbackSlot = configuredSlots[0];
            long lastFallbackUse = _localSkillLastUseMs.GetValueOrDefault(fallbackSlot);
            return nowMs - lastFallbackUse >= 350 ? fallbackSlot : -1;
        }

        foreach (int slot in configuredSlots)
        {
            var skill = player.SkillList.FirstOrDefault(s => s.Slot == slot);
            if (skill == null) continue;

            long lastUse = Math.Max(skill.LastTimeUse, _localSkillLastUseMs.GetValueOrDefault(slot));
            int cooldown = Math.Max(skill.CoolDown, 200);
            if (nowMs - lastUse >= cooldown && (skill.ManaUse <= player.Mp || slot == 0))
            {
                return slot;
            }
        }

        return -1;
    }

    private async Task<bool> TryChangeZoneAsync(
        WorldSnapshot world,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (DateTime.UtcNow - _lastZoneChangeTime < TimeSpan.FromSeconds(5))
            return false;

        if (world.Zones.Count == 0)
        {
            if (DateTime.UtcNow - _lastZoneOpenRequestTime > TimeSpan.FromSeconds(3))
            {
                _lastZoneOpenRequestTime = DateTime.UtcNow;
                await dispatcher.DispatchAsync(new OpenZonePanelCommand(), cancellationToken);
                await Task.Delay(250, cancellationToken);
                await dispatcher.DispatchAsync(new CloseZonePanelCommand(), cancellationToken);
            }
            return false;
        }

        var candidateZones = world.Zones
            .Where(z => z.ZoneId != world.ZoneId && !z.IsFull)
            .OrderBy(z => z.NumPlayers)
            .ThenBy(z => z.ZoneId)
            .ToList();

        if (candidateZones.Count == 0)
            return false;

        int nextZoneId = candidateZones[0].ZoneId;
        _lastZoneChangeTime = DateTime.UtcNow;
        var result = await dispatcher.DispatchAsync(new ChangeZoneCommand(nextZoneId), cancellationToken);
        if (result.IsSuccess)
        {
            progress?.Report($"[Auto Train] Hết quái, đổi sang khu {nextZoneId}.");
            return true;
        }

        return false;
    }
}
