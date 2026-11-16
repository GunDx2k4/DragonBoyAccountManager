using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Coordinators;

public sealed class GroundLootCoordinator : IGroundLootCoordinator
{
    private const int PickCooldownMs = 350;
    private const int MoveRefreshMs = 600;
    private const int MaxItemPickAttempts = 3;
    private const int ItemApproachTimeoutMs = 3000;
    private const int ItemBlacklistDurationMs = 15000;
    private const int PickRangePx = 32;
    private const int LocalPickRadiusPx = 200;

    private readonly Dictionary<int, DateTime> _itemBlacklist = [];
    private readonly List<int> _expiredItemKeys = [];
    private int _currentItemId = -1;
    private int _currentItemPickAttempts;
    private double _lastItemDist = double.MaxValue;
    private DateTime _itemApproachStartTime = DateTime.MinValue;
    private DateTime _lastPickTime = DateTime.MinValue;
    private DateTime _lastMoveTime = DateTime.MinValue;
    private int _lastMoveX;
    private int _lastMoveY;

    public void Reset()
    {
        _itemBlacklist.Clear();
        _expiredItemKeys.Clear();
        _currentItemId = -1;
        _currentItemPickAttempts = 0;
        _lastItemDist = double.MaxValue;
        _itemApproachStartTime = DateTime.MinValue;
        _lastPickTime = DateTime.MinValue;
        _lastMoveTime = DateTime.MinValue;
        _lastMoveX = 0;
        _lastMoveY = 0;
    }

    public void BlacklistItem(int itemMapId, DateTime now)
    {
        _itemBlacklist[itemMapId] = now.AddMilliseconds(ItemBlacklistDurationMs);
        _currentItemPickAttempts = 0;
        _currentItemId = -1;
        _lastItemDist = double.MaxValue;
    }

    public async Task<bool> TryPickGroundItemsAsync(
        ISessionContext context,
        PlayerSnapshot player,
        WorldSnapshot world,
        LootFilterOptions options,
        string logPrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!options.AutoPick || world.GroundItems.Count == 0)
        {
            _currentItemId = -1;
            _currentItemPickAttempts = 0;
            _lastItemDist = double.MaxValue;
            return false;
        }

        var now = DateTime.UtcNow;
        PruneExpiredBlacklist(now);

        bool hasMagnetCharm = player.HasMagnetCharm;
        GroundItemSnapshot? closest = null;
        double closestDist = double.MaxValue;

        for (int i = 0; i < world.GroundItems.Count; i++)
        {
            var item = world.GroundItems[i];
            if (_itemBlacklist.ContainsKey(item.ItemMapId))
            {
                continue;
            }

            bool isAllowedOwner = options.PickOthersItems || item.PlayerId == player.Id || item.PlayerId == -1;
            if (!isAllowedOwner)
            {
                continue;
            }

            if (hasMagnetCharm && item.PlayerId == player.Id && !options.PickAllMap)
            {
                continue;
            }

            if (options.ExcludeIds != null && options.ExcludeIds.Contains(item.TemplateId))
            {
                continue;
            }

            if (options.IncludeIds != null && options.IncludeIds.Count > 0 && !options.IncludeIds.Contains(item.TemplateId))
            {
                continue;
            }

            if (options.NameFilters != null && options.NameFilters.Count > 0)
            {
                bool matchedFilter = false;
                for (int n = 0; n < options.NameFilters.Count; n++)
                {
                    string f = options.NameFilters[n];
                    if (item.Name.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        item.TemplateId.ToString() == f)
                    {
                        matchedFilter = true;
                        break;
                    }
                }

                if (!matchedFilter)
                {
                    continue;
                }
            }

            double dist = item.DistanceTo(player.X, player.Y);
            if (!options.PickAllMap && dist > LocalPickRadiusPx)
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
            _lastItemDist = double.MaxValue;
            return false;
        }

        if (closest.ItemMapId != _currentItemId)
        {
            _currentItemId = closest.ItemMapId;
            _currentItemPickAttempts = 0;
            _itemApproachStartTime = now;
            _lastItemDist = closestDist;
        }
        else if (closestDist < _lastItemDist - 10)
        {
            _lastItemDist = closestDist;
            _itemApproachStartTime = now;
        }

        var dest = context.CurrentMap.IsValid
            ? context.CurrentMap.ResolveSafePosition(closest.X, closest.Y, preferGround: true)
            : (X: closest.X, Y: closest.Y);

        int dx = Math.Abs(player.X - dest.X);
        int dy = Math.Abs(player.Y - dest.Y);
        bool inPickRange = (closestDist <= PickRangePx) || (dx <= 36 && dy <= 40);

        if (!inPickRange)
        {
            if ((now - _itemApproachStartTime).TotalMilliseconds > ItemApproachTimeoutMs)
            {
                BlacklistItem(closest.ItemMapId, now);
                return false;
            }

            await MoveTowardAsync(context, dest.X, dest.Y, options.IsFastTeleport, now, cancellationToken);
            progress?.Report($"{logPrefix} Chạy tới nhặt {closest.Name}...");
            return true;
        }

        _itemApproachStartTime = now;
        if ((now - _lastPickTime).TotalMilliseconds >= PickCooldownMs)
        {
            _currentItemPickAttempts++;
            if (_currentItemPickAttempts > MaxItemPickAttempts)
            {
                BlacklistItem(closest.ItemMapId, now);
                progress?.Report($"{logPrefix} Bỏ qua nhặt {closest.Name} (túi đầy hoặc kẹt).");
                return false;
            }

            _lastPickTime = now;
            await context.Dispatcher.DispatchAsync(
                new PickItemCommand(closest.ItemMapId, closest.Name),
                cancellationToken);
            progress?.Report($"{logPrefix} Nhặt {closest.Name}.");
        }

        return true;
    }

    private async Task MoveTowardAsync(
        ISessionContext context,
        int targetX,
        int targetY,
        bool isFastTeleport,
        DateTime now,
        CancellationToken cancellationToken)
    {
        bool sameTarget = Math.Abs(targetX - _lastMoveX) <= 20 && Math.Abs(targetY - _lastMoveY) <= 20;
        if (!isFastTeleport && sameTarget && (now - _lastMoveTime).TotalMilliseconds < MoveRefreshMs)
        {
            return;
        }

        _lastMoveX = targetX;
        _lastMoveY = targetY;
        _lastMoveTime = now;

        await context.Dispatcher.DispatchAsync(
            new NavigateCommand(targetX, targetY, isFastTeleport),
            cancellationToken);
    }

    private void PruneExpiredBlacklist(DateTime now)
    {
        if (_itemBlacklist.Count == 0)
        {
            return;
        }

        _expiredItemKeys.Clear();
        foreach (var pair in _itemBlacklist)
        {
            if (now >= pair.Value)
            {
                _expiredItemKeys.Add(pair.Key);
            }
        }

        for (int i = 0; i < _expiredItemKeys.Count; i++)
        {
            _itemBlacklist.Remove(_expiredItemKeys[i]);
        }
    }
}
