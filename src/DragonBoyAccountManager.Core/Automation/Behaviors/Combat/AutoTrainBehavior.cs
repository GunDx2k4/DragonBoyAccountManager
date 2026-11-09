using DragonBoyAccountManager.Core.Automation.Interrupters;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed class AutoTrainBehavior : AutomationBehavior<AutoTrainOptions>
{
    public const string BehaviorId = "AutoTrain";
    private const long AutoTdltCooldownMs = 2000;

    private readonly AutoReviveInterrupter _reviveInterrupter = new();
    private readonly AutoPeanInterrupter _peanInterrupter = new();
    private readonly Dictionary<int, long> _localSkillLastUseMs = new();
    private readonly Dictionary<int, long> _ignoredItemMapIds = new();
    private long _lastTdltToggleMs;
    private bool _inGameAutoDisabled;
    private int _waitingSpawnMobId = -1;
    private int _lastPickItemMapId = -1;
    private int _pickRetryCount;
    private int _pickMoveAttempts;
    private long _lastTimePickMs;
    private DateTime _lastZoneChangeTime = DateTime.MinValue;
    private DateTime _lastZoneOpenRequestTime = DateTime.MinValue;

    public AutoTrainBehavior(AutoTrainOptions options)
        : base("Auto Train", options, BehaviorId)
    {
        Interrupters.Register(_reviveInterrupter);
        Interrupters.Register(_peanInterrupter);
        ApplyInterrupterOptions(options);
    }

    private void ApplyInterrupterOptions(AutoTrainOptions options)
    {
        _peanInterrupter.Enabled = options.AutoUsePean;
        _peanInterrupter.MinHpPercent = options.MinHpPercent;
        _peanInterrupter.MinMpPercent = options.MinMpPercent;
    }

    protected override void OnOptionsChanged(AutoTrainOptions previousOptions, AutoTrainOptions newOptions)
    {
        ApplyInterrupterOptions(newOptions);
        _inGameAutoDisabled = false;

        if (Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            bool hadTdlt = previousOptions.SelectedItemTemplateIds?.Contains(GameDefinitions.DefaultTdltTemplateId) == true;
            bool hasTdlt = newOptions.SelectedItemTemplateIds?.Contains(GameDefinitions.DefaultTdltTemplateId) == true;
            string reason = IsRunning ? "DBAM_Takeover" : "DBAM_SaveConfig";
            var cmd = BuildTakeoverCommand(newOptions, reason);
            _ = Task.Run(async () =>
            {
                await ctx.Dispatcher.DispatchAsync(cmd, CancellationToken.None);
                if (!IsRunning && hadTdlt && !hasTdlt && ctx.CurrentPlayer.IsTdltActive)
                {
                    await ctx.Dispatcher.DispatchAsync(
                        new UseItemCommand(-1, GameDefinitions.DefaultTdltTemplateId, "Tự động luyện tập"),
                        CancellationToken.None);
                }
            });
        }
    }

    protected override void OnStarted(IProgress<string>? progress)
    {
        ApplyInterrupterOptions(Options);
        _inGameAutoDisabled = false;
        _waitingSpawnMobId = -1;
        _lastPickItemMapId = -1;
        _pickRetryCount = 0;
        _pickMoveAttempts = 0;
        _lastTimePickMs = 0;
        _lastTdltToggleMs = 0;
        _ignoredItemMapIds.Clear();
        _localSkillLastUseMs.Clear();
        progress?.Report("[Auto Train] Bắt đầu tự động luyện tập...");
    }

    protected override void OnStopped(IProgress<string>? progress)
    {
        if (Context is { IsRunning: true, IsQuitRequested: false } ctx)
        {
            var selectedItems = Options.SelectedItemTemplateIds;
            bool hasTdlt = selectedItems?.Contains(GameDefinitions.DefaultTdltTemplateId) == true;
            _ = Task.Run(async () =>
            {
                await ctx.Dispatcher.DispatchAsync(
                    new SetInGameAutoTrainCommand(false, "DBAM_Stop", SelectedItemTemplateIds: selectedItems),
                    CancellationToken.None);
                if (hasTdlt && ctx.CurrentPlayer.IsTdltActive)
                {
                    await ctx.Dispatcher.DispatchAsync(
                        new UseItemCommand(-1, GameDefinitions.DefaultTdltTemplateId, "Tự động luyện tập"),
                        CancellationToken.None);
                }
            });
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
            await dispatcher.DispatchAsync(BuildTakeoverCommand(Options), cancellationToken);
        }

        await TryManageTdltAsync(player, dispatcher, progress, cancellationToken);

        if (await TryPickItemAsync(player, world, dispatcher, progress, cancellationToken))
        {
            return;
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

    private async Task TryManageTdltAsync(
        PlayerSnapshot player,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (nowMs - _lastTdltToggleMs < AutoTdltCooldownMs)
            return;

        var selectedIds = Options.SelectedItemTemplateIds ?? [];
        bool wantTdlt = selectedIds.Contains(GameDefinitions.DefaultTdltTemplateId);
        if (wantTdlt != player.IsTdltActive)
        {
            var tdltItem = player.BagItemList.FirstOrDefault(i => i.Quantity >= 0 && (i.TemplateId == GameDefinitions.DefaultTdltTemplateId || i.IsTdlt));
            if (tdltItem != null)
            {
                _lastTdltToggleMs = nowMs;
                var res = await dispatcher.DispatchAsync(new UseItemCommand(tdltItem.Index, tdltItem.TemplateId, tdltItem.Name), cancellationToken);
                if (res.IsSuccess)
                {
                    progress?.Report(wantTdlt
                        ? $"[Auto Train] Tự động bật {tdltItem.Name}."
                        : $"[Auto Train] Tự động tắt {tdltItem.Name}.");
                }
            }
        }
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

    private async Task<bool> TryPickItemAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        ICommandDispatcher dispatcher,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!Options.AutoPick || world.GroundItems.Count == 0)
        {
            return false;
        }

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var excludeSet = ParseIdSet(Options.FilterExcludeIds);
        var pickSet = ParseIdSet(Options.FilterPickIds);

        GroundItemSnapshot? targetItem = null;
        double minDistance = double.MaxValue;

        foreach (var item in world.GroundItems)
        {
            if (item.TemplateId < 0)
            {
                continue;
            }

            if (_ignoredItemMapIds.TryGetValue(item.ItemMapId, out long ignoreUntil))
            {
                if (now < ignoreUntil)
                {
                    continue;
                }
                _ignoredItemMapIds.Remove(item.ItemMapId);
            }

            if (!Options.PickOthersItems && item.PlayerId != player.Id && item.PlayerId != -1)
            {
                continue;
            }

            if (excludeSet.Count > 0 && excludeSet.Contains(item.TemplateId))
            {
                continue;
            }

            if (pickSet.Count > 0 && !pickSet.Contains(item.TemplateId))
            {
                continue;
            }

            double dist = item.DistanceTo(player.X, player.Y);
            if (!Options.PickAllMap && dist > 200)
            {
                continue;
            }

            if (dist < minDistance)
            {
                minDistance = dist;
                targetItem = item;
            }
        }

        if (targetItem == null)
        {
            _lastPickItemMapId = -1;
            _pickRetryCount = 0;
            _pickMoveAttempts = 0;
            return false;
        }

        if (_lastPickItemMapId != targetItem.ItemMapId)
        {
            _lastPickItemMapId = targetItem.ItemMapId;
            _pickRetryCount = 0;
            _pickMoveAttempts = 0;
        }

        if (minDistance > 30)
        {
            _pickMoveAttempts++;
            if (_pickMoveAttempts > 20)
            {
                _ignoredItemMapIds[targetItem.ItemMapId] = now + 10000;
                _lastPickItemMapId = -1;
                _pickMoveAttempts = 0;
                _pickRetryCount = 0;
                return false;
            }

            if (Options.PickAllMap || player.IsTdltActive)
            {
                await dispatcher.DispatchAsync(new MoveCommand(targetItem.X, targetItem.Y, IsFastTeleport: true), cancellationToken);
                await Task.Delay(50, cancellationToken);
                return true;
            }

            await dispatcher.DispatchAsync(new MoveCommand(targetItem.X, targetItem.Y, IsFastTeleport: false), cancellationToken);
            await Task.Delay(200, cancellationToken);
            return true;
        }

        _pickMoveAttempts = 0;
        if (now - _lastTimePickMs < 350)
        {
            await Task.Delay(80, cancellationToken);
            return true;
        }

        _pickRetryCount++;
        if (_pickRetryCount > 5)
        {
            _ignoredItemMapIds[targetItem.ItemMapId] = now + 10000;
            _lastPickItemMapId = -1;
            _pickRetryCount = 0;
            return false;
        }

        _lastTimePickMs = now;
        var res = await dispatcher.DispatchAsync(new PickItemCommand(targetItem.ItemMapId, targetItem.Name), cancellationToken);
        if (res.IsSuccess)
        {
            progress?.Report($"[Auto Train] Nhặt vật phẩm: {targetItem.Name} #{targetItem.ItemMapId}");
        }
        await Task.Delay(150, cancellationToken);
        return true;
    }

    private static HashSet<int> ParseIdSet(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var set = new HashSet<int>();
        var tokens = raw.Split([',', ' ', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            if (int.TryParse(token.Trim(), out int id))
            {
                set.Add(id);
            }
        }

        return set;
    }

    private static SetInGameAutoTrainCommand BuildTakeoverCommand(AutoTrainOptions options, string reason = "DBAM_Takeover") =>
        new(
            Enable: false,
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
            AutoPick: options.AutoPick,
            PickOthersItems: options.PickOthersItems,
            PickAllMap: options.PickAllMap,
            AutoDropItems: options.AutoDropItems,
            FilterPickIds: options.FilterPickIds,
            FilterExcludeIds: options.FilterExcludeIds,
            FilterDropIds: options.FilterDropIds);
}
