using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Interrupters;

public class AutoPeanInterrupter : IInterrupter
{
    private DateTime _lastPeanTime = DateTime.MinValue;

    public string Name => "AutoPean";
    public bool Enabled { get; set; } = true;
    public int MinHpPercent { get; set; } = 20;
    public int MinMpPercent { get; set; } = 20;
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(2);

    public AutoPeanInterrupter(bool enabled = true, int minHpPercent = 20, int minMpPercent = 20)
    {
        Enabled = enabled;
        MinHpPercent = minHpPercent;
        MinMpPercent = minMpPercent;
    }

    public async Task<Result?> CheckAndExecuteAsync(
        PlayerSnapshot player,
        WorldSnapshot world,
        ICommandDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (!Enabled || !player.IsInGame || !player.IsAlive)
            return null;

        if (DateTime.UtcNow - _lastPeanTime < Cooldown)
            return null;

        bool needHp = player.MaxHp > 0 && (player.Hp * 100 / player.MaxHp) <= MinHpPercent;
        bool needMp = player.MaxMp > 0 && (player.Mp * 100 / player.MaxMp) <= MinMpPercent;

        if (needHp || needMp)
        {
            var res = await dispatcher.DispatchAsync(new UseItemCommand(-1, 13, "Đậu thần"), cancellationToken);
            if (res.IsSuccess)
            {
                _lastPeanTime = DateTime.UtcNow;
                return Result.Success("Đã tự động sử dụng đậu thần hồi phục.");
            }
        }

        return null;
    }
}
