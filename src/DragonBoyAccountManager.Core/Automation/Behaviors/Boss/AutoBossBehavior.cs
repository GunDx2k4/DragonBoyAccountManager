using DragonBoyAccountManager.Core.Automation.Coordinators;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Behaviors.Boss;

public sealed class AutoBossBehavior : AutomationBehavior<AutoBossOptions>
{
    public const string BehaviorId = "AutoBoss";

    private readonly HashSet<int> _visitedZones = [];
    private readonly IGroundLootCoordinator _lootCoordinator;
    private IReadOnlyList<string> _cachedNameFilters = [];
    private int _currentMapIndex;
    private int _lastMapId = -1;
    private DateTime _lastCombatTime = DateTime.MinValue;
    private DateTime _lastZoneHopTime = DateTime.MinValue;
    private DateTime _lastZonePanelTime = DateTime.MinValue;

    public AutoBossBehavior(AutoBossOptions options, IGroundLootCoordinator? lootCoordinator = null)
        : base("Săn Boss", options, BehaviorId)
    {
        _lootCoordinator = lootCoordinator ?? new GroundLootCoordinator();
        _cachedNameFilters = ParseNameFilters(options.FilterPickNames);
    }

    protected override int TickDelayMs => 500;

    protected override void OnStarted(IProgress<string>? progress)
    {
        _cachedNameFilters = ParseNameFilters(Options.FilterPickNames);
        ResetScanState();
        progress?.Report("[Săn Boss] Đã bật tự động săn Boss.");
    }

    protected override void OnStopped(IProgress<string>? progress)
    {
        ResetScanState();
        progress?.Report("[Săn Boss] Đã dừng tự động săn Boss.");
    }

    protected override void OnOptionsChanged(AutoBossOptions previousOptions, AutoBossOptions newOptions)
    {
        _cachedNameFilters = ParseNameFilters(newOptions.FilterPickNames);
        if (previousOptions.TargetBossName != newOptions.TargetBossName || previousOptions.MinZone != newOptions.MinZone)
        {
            _visitedZones.Clear();
        }
    }

    private void ResetScanState()
    {
        _currentMapIndex = 0;
        _lastMapId = -1;
        _visitedZones.Clear();
        _lootCoordinator.Reset();
        _lastCombatTime = DateTime.MinValue;
        _lastZoneHopTime = DateTime.MinValue;
        _lastZonePanelTime = DateTime.MinValue;
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

        var mapIds = Options.SelectedMapIds;
        if (mapIds.Count == 0)
        {
            progress?.Report("[Săn Boss] Chưa chọn bản đồ săn Boss.");
            await Task.Delay(1000, cancellationToken);
            return;
        }

        var world = context.CurrentWorld;
        if (_lastMapId == -1)
        {
            int idx = -1;
            for (int i = 0; i < mapIds.Count; i++)
            {
                if (mapIds[i] == world.MapId)
                {
                    idx = i;
                    break;
                }
            }
            if (idx >= 0)
            {
                _currentMapIndex = idx;
            }
        }

        if (world.MapId != _lastMapId)
        {
            _lastMapId = world.MapId;
            _visitedZones.Clear();
            _lastCombatTime = DateTime.MinValue;
        }

        if (_currentMapIndex >= mapIds.Count)
        {
            _currentMapIndex = 0;
        }

        int targetMapId = mapIds[_currentMapIndex];
        if (world.MapId != targetMapId)
        {
            progress?.Report($"[Săn Boss] Đang di chuyển tới {WorldMapGraph.GetMapName(targetMapId)} (#{targetMapId})...");
            await context.Dispatcher.DispatchAsync(new XmapMoveCommand(targetMapId, Options.IsFastTeleport), cancellationToken);
            await Task.Delay(500, cancellationToken);
            return;
        }

        var lootOptions = new LootFilterOptions(
            AutoPick: true,
            PickOthersItems: Options.PickOthersItems,
            PickAllMap: false,
            IsFastTeleport: Options.IsFastTeleport,
            NameFilters: _cachedNameFilters);

        if (await _lootCoordinator.TryPickGroundItemsAsync(context, player, world, lootOptions, "[Săn Boss]", progress, cancellationToken))
        {
            return;
        }

        if (await TryEngageBossAsync(context, player, world, progress, cancellationToken))
        {
            return;
        }

        if (_lastCombatTime != DateTime.MinValue && (DateTime.UtcNow - _lastCombatTime).TotalMilliseconds < 1500)
        {
            progress?.Report($"[Săn Boss] Chờ vật phẩm rơi (Khu {world.ZoneId})...");
            return;
        }

        if (world.ZoneId >= 0)
        {
            _visitedZones.Add(world.ZoneId);
        }

        await ScanNextZoneAsync(context, world, mapIds, progress, cancellationToken);
    }

    private static IReadOnlyList<string> ParseNameFilters(string? rawFilters)
    {
        if (string.IsNullOrWhiteSpace(rawFilters))
        {
            return [];
        }

        return rawFilters.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private async Task<bool> TryEngageBossAsync(
        ISessionContext context,
        PlayerSnapshot player,
        WorldSnapshot world,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        string targetName = Options.TargetBossName?.Trim() ?? string.Empty;

        WorldBossInfo? bestBoss = null;
        int bestDist = int.MaxValue;
        for (int i = 0; i < world.Bosses.Count; i++)
        {
            var b = world.Bosses[i];
            if (b.Hp <= 0)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(targetName) && !b.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            int dist = Math.Abs(b.X - player.X) + Math.Abs(b.Y - player.Y);
            if (dist < bestDist)
            {
                bestDist = dist;
                bestBoss = b;
            }
        }

        if (bestBoss != null)
        {
            await EngageTargetAsync(context, player, bestBoss.X, bestBoss.Y, bestBoss.Id, bestBoss.Name, bestBoss.Hp, isMob: false, world.ZoneId, progress, cancellationToken);
            return true;
        }

        if (!string.IsNullOrEmpty(targetName))
        {
            WorldPlayerInfo? bestPlayerBoss = null;
            bestDist = int.MaxValue;
            for (int i = 0; i < world.Players.Count; i++)
            {
                var p = world.Players[i];
                if (p.Id == player.Id || p.Hp <= 0)
                {
                    continue;
                }

                if (!p.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int dist = Math.Abs(p.X - player.X) + Math.Abs(p.Y - player.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPlayerBoss = p;
                }
            }

            if (bestPlayerBoss != null)
            {
                await EngageTargetAsync(context, player, bestPlayerBoss.X, bestPlayerBoss.Y, bestPlayerBoss.Id, bestPlayerBoss.Name, bestPlayerBoss.Hp, isMob: false, world.ZoneId, progress, cancellationToken);
                return true;
            }

            WorldMobInfo? bestMobBoss = null;
            bestDist = int.MaxValue;
            for (int i = 0; i < world.Mobs.Count; i++)
            {
                var m = world.Mobs[i];
                if (!m.IsAlive || m.Hp <= 0)
                {
                    continue;
                }

                if (!m.IsBoss && !m.Name.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int dist = Math.Abs(m.X - player.X) + Math.Abs(m.Y - player.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestMobBoss = m;
                }
            }

            if (bestMobBoss != null)
            {
                await EngageTargetAsync(context, player, bestMobBoss.X, bestMobBoss.Y, bestMobBoss.Id, bestMobBoss.Name, bestMobBoss.Hp, isMob: true, world.ZoneId, progress, cancellationToken);
                return true;
            }
        }

        return false;
    }

    private async Task EngageTargetAsync(
        ISessionContext context,
        PlayerSnapshot player,
        int targetX,
        int targetY,
        int targetId,
        string targetName,
        long targetHp,
        bool isMob,
        int zoneId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        int dx = Math.Abs(player.X - targetX);
        int dy = Math.Abs(player.Y - targetY);
        if (dx > 60 || dy > 60)
        {
            progress?.Report($"[Săn Boss] Áp sát {targetName} ({targetX}, {targetY})...");
            await context.Dispatcher.DispatchAsync(new NavigateCommand(targetX, targetY, Options.IsFastTeleport), cancellationToken);
            return;
        }

        int slot = SelectReadySkillSlot(player.SkillList, Options.SelectedSkillSlots);
        if (isMob)
        {
            await context.Dispatcher.DispatchAsync(new AttackMobCommand(targetId, targetName, targetX, slot), cancellationToken);
        }
        else
        {
            await context.Dispatcher.DispatchAsync(new AttackBossCommand(targetId, targetName, targetX, slot), cancellationToken);
        }

        _lastCombatTime = DateTime.UtcNow;
        progress?.Report($"[Săn Boss] Đánh {targetName} (HP: {targetHp:N0}) - Khu {zoneId}");
    }

    private async Task ScanNextZoneAsync(
        ISessionContext context,
        WorldSnapshot world,
        IReadOnlyList<int> mapIds,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (world.Zones.Count == 0)
        {
            if ((DateTime.UtcNow - _lastZonePanelTime).TotalSeconds >= 2)
            {
                _lastZonePanelTime = DateTime.UtcNow;
                progress?.Report($"[Săn Boss] Tải danh sách khu {world.MapName}...");
                await context.Dispatcher.DispatchAsync(new OpenZonePanelCommand(), cancellationToken);
            }
            return;
        }

        int minZone = Math.Max(0, Options.MinZone);
        WorldZoneInfo? nextZone = null;
        for (int i = 0; i < world.Zones.Count; i++)
        {
            var z = world.Zones[i];
            if (z.ZoneId < minZone || z.IsFull || z.ZoneId == world.ZoneId || _visitedZones.Contains(z.ZoneId))
            {
                continue;
            }

            if (nextZone == null || z.ZoneId < nextZone.ZoneId)
            {
                nextZone = z;
            }
        }

        if (nextZone == null)
        {
            if (mapIds.Count == 1)
            {
                _visitedZones.Clear();
                if (world.ZoneId >= 0)
                {
                    _visitedZones.Add(world.ZoneId);
                }
                progress?.Report($"[Săn Boss] Quét lại danh sách khu {world.MapName}...");
                await context.Dispatcher.DispatchAsync(new OpenZonePanelCommand(), cancellationToken);
                return;
            }

            _currentMapIndex = (_currentMapIndex + 1) % mapIds.Count;
            _visitedZones.Clear();
            int nextMapId = mapIds[_currentMapIndex];
            progress?.Report($"[Săn Boss] Hết khu, chuyển sang {WorldMapGraph.GetMapName(nextMapId)} (#{nextMapId})...");
            await context.Dispatcher.DispatchAsync(new XmapMoveCommand(nextMapId, Options.IsFastTeleport), cancellationToken);
            return;
        }

        double delaySec = Math.Clamp(Options.ZoneChangeDelaySeconds, 1, 120);
        double elapsed = (DateTime.UtcNow - _lastZoneHopTime).TotalSeconds;
        if (_lastZoneHopTime != DateTime.MinValue && elapsed < delaySec)
        {
            int remain = (int)Math.Ceiling(delaySec - elapsed);
            progress?.Report($"[Săn Boss] Đợi {remain}s đổi sang khu {nextZone.ZoneId}...");
            return;
        }

        _lastZoneHopTime = DateTime.UtcNow;
        progress?.Report($"[Săn Boss] Đổi sang {nextZone.DisplayText}...");
        await context.Dispatcher.DispatchAsync(new ChangeZoneCommand(nextZone.ZoneId), cancellationToken);
    }

    private static int SelectReadySkillSlot(IReadOnlyList<GameSkillInfo> skills, IReadOnlyList<int> preferredSlots)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (preferredSlots.Count > 0)
        {
            for (int p = 0; p < preferredSlots.Count; p++)
            {
                int slot = preferredSlots[p];
                for (int i = 0; i < skills.Count; i++)
                {
                    var skill = skills[i];
                    if (skill.Slot == slot && skill.IsReady(now))
                    {
                        return slot;
                    }
                }
            }
            return preferredSlots[0];
        }

        for (int i = 0; i < skills.Count; i++)
        {
            var readySkill = skills[i];
            if (readySkill.IsReady(now))
            {
                return readySkill.Slot;
            }
        }

        return 0;
    }
}
