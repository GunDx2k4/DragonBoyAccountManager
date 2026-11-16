using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Coordinators;

public sealed class RecoveryCoordinator : IRecoveryCoordinator
{
    private const int PeanCooldownMs = 1000;
    private const int ChargeMaxDurationMs = 8000;

    private DateTime _lastPeanTime = DateTime.MinValue;
    private DateTime _chargeStartTime = DateTime.MinValue;

    public void Reset()
    {
        _lastPeanTime = DateTime.MinValue;
        _chargeStartTime = DateTime.MinValue;
    }

    public async Task<bool> TryUsePeanAsync(
        ISessionContext context,
        PlayerSnapshot player,
        RecoveryOptions options,
        string logPrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        if (!options.AutoUsePean || player.MaxHp <= 0 || player.MaxMp <= 0)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastPeanTime).TotalMilliseconds < PeanCooldownMs)
        {
            return false;
        }

        long hpPercent = player.Hp * 100 / player.MaxHp;
        long mpPercent = player.Mp * 100 / player.MaxMp;
        if (hpPercent >= options.MinHpPercent && mpPercent >= options.MinMpPercent)
        {
            return false;
        }

        InventoryItem? pean = null;
        var bagItems = player.BagItemList;
        for (int i = 0; i < bagItems.Count; i++)
        {
            var item = bagItems[i];
            if (item.IsPean && item.Quantity > 0)
            {
                pean = item;
                break;
            }
        }
        if (pean == null)
        {
            return false;
        }

        _lastPeanTime = now;
        progress?.Report($"{logPrefix} Dùng {pean.Name} (HP: {hpPercent}%, MP: {mpPercent}%).");

        await context.Dispatcher.DispatchAsync(
            new UseItemCommand(pean.Index, pean.TemplateId, pean.Name),
            cancellationToken);

        return true;
    }

    public async Task<bool> HandleChargingStateAsync(
        ISessionContext context,
        PlayerSnapshot player,
        CancellationToken cancellationToken)
    {
        if (!player.IsCharging)
        {
            _chargeStartTime = DateTime.MinValue;
            return false;
        }

        var now = DateTime.UtcNow;
        if (_chargeStartTime == DateTime.MinValue)
        {
            _chargeStartTime = now;
        }

        bool hpRecovered = player.MaxHp <= 0 || player.Hp * 100 / player.MaxHp >= 95;
        bool mpRecovered = player.MaxMp <= 0 || player.Mp * 100 / player.MaxMp >= 95;
        if ((!hpRecovered || !mpRecovered) && (now - _chargeStartTime).TotalMilliseconds < ChargeMaxDurationMs)
        {
            return true;
        }

        _chargeStartTime = DateTime.MinValue;
        await context.Dispatcher.DispatchAsync(new StopSkillChargeCommand(), cancellationToken);
        return false;
    }
}
