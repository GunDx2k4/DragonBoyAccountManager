using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Coordinators;

public sealed class ZoneCoordinator : IZoneCoordinator
{
    private const int EmptyMapZoneDelayMs = 8000;
    private const int EmptyMapNoEligibleMobsDelayMs = 2500;
    private const int ZoneChangeCooldownMs = 5000;
    private const int ZonePanelCooldownMs = 2000;
    private const int ZoneListRefreshIntervalMs = 10000;
    private const int ZonePanelWaitMs = 600;

    private readonly HashSet<int> _visitedZones = [];
    private DateTime _mapEmptySince = DateTime.MinValue;
    private DateTime _lastZoneHopTime = DateTime.MinValue;
    private DateTime _lastZonePanelTime = DateTime.MinValue;
    private DateTime _zonePanelRequestedAt = DateTime.MinValue;
    private DateTime _zonesRefreshedAt = DateTime.MinValue;

    public void Reset()
    {
        _visitedZones.Clear();
        _mapEmptySince = DateTime.MinValue;
        _lastZoneHopTime = DateTime.MinValue;
        _lastZonePanelTime = DateTime.MinValue;
        _zonePanelRequestedAt = DateTime.MinValue;
        _zonesRefreshedAt = DateTime.MinValue;
    }

    public void OnMapChanged(int newMapId)
    {
        _visitedZones.Clear();
        _mapEmptySince = DateTime.MinValue;
        _zonePanelRequestedAt = DateTime.MinValue;
        _zonesRefreshedAt = DateTime.MinValue;
    }

    public async Task<bool> TryHopZoneAsync(
        ISessionContext context,
        WorldSnapshot world,
        ZoneHopOptions options,
        string logPrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!options.ChangeZoneWhenNoMobs)
        {
            return false;
        }

        if (!options.CheckAllMobs && options.TargetMobNames.Count == 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;

        if (_mapEmptySince == DateTime.MinValue)
        {
            _mapEmptySince = now;
            return false;
        }

        int emptyDelayMs = HasAnyMatchingMobInMap(world, options)
            ? EmptyMapZoneDelayMs
            : EmptyMapNoEligibleMobsDelayMs;

        if ((now - _mapEmptySince).TotalMilliseconds < emptyDelayMs)
        {
            return false;
        }

        bool justRefreshed = false;
        if (_zonePanelRequestedAt != DateTime.MinValue)
        {
            if ((now - _zonePanelRequestedAt).TotalMilliseconds < ZonePanelWaitMs)
            {
                return false;
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
            return false;
        }

        if (world.ZoneId >= 0)
        {
            _visitedZones.Add(world.ZoneId);
        }

        WorldZoneInfo? nextZone = null;
        if (world.Zones.Count > 1)
        {
            for (int i = 0; i < world.Zones.Count; i++)
            {
                var z = world.Zones[i];
                if (z.IsFull || z.ZoneId == world.ZoneId || _visitedZones.Contains(z.ZoneId))
                {
                    continue;
                }

                if (nextZone == null
                    || z.NumPlayers < nextZone.NumPlayers
                    || (z.NumPlayers == nextZone.NumPlayers && z.ZoneId < nextZone.ZoneId))
                {
                    nextZone = z;
                }
            }
        }

        if (nextZone == null)
        {
            _visitedZones.Clear();
            if (world.ZoneId >= 0)
            {
                _visitedZones.Add(world.ZoneId);
            }

            _mapEmptySince = now;
            _zonesRefreshedAt = DateTime.MinValue;
            return false;
        }

        if ((now - _lastZoneHopTime).TotalMilliseconds < ZoneChangeCooldownMs)
        {
            return false;
        }

        _lastZoneHopTime = now;
        _mapEmptySince = DateTime.MinValue;
        _zonesRefreshedAt = DateTime.MinValue;
        _zonePanelRequestedAt = DateTime.MinValue;
        progress?.Report($"{logPrefix} Hết quái, chuyển sang {nextZone.DisplayText}...");

        await context.Dispatcher.DispatchAsync(new ChangeZoneCommand(nextZone.ZoneId), cancellationToken);
        return true;
    }

    private static bool HasAnyMatchingMobInMap(WorldSnapshot world, ZoneHopOptions options)
    {
        for (int i = 0; i < world.Mobs.Count; i++)
        {
            var mob = world.Mobs[i];
            if (!MatchesMobBaseCriteria(mob, options))
            {
                continue;
            }

            if (options.CheckAttackByHp && mob.MaxHp > 0)
            {
                if (options.MinMobHp > 0 && mob.MaxHp < options.MinMobHp)
                {
                    continue;
                }

                if (options.MaxMobHp > 0 && mob.MaxHp > options.MaxMobHp)
                {
                    continue;
                }
            }

            return true;
        }

        return false;
    }

    private static bool MatchesMobBaseCriteria(WorldMobInfo mob, ZoneHopOptions options)
    {
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
}
