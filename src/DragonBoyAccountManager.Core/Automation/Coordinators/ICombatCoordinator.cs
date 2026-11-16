using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Automation.Coordinators;

public readonly record struct CombatOptions(
    bool AvoidSuperMobs,
    bool CheckAllMobs,
    IReadOnlyList<string> TargetMobNames,
    bool CheckAttackByHp,
    long MinMobHp,
    long MaxMobHp,
    IReadOnlyList<int> SelectedSkillSlots,
    bool AutoUsePean,
    int MinHpPercent,
    int MinMpPercent);

public interface ICombatCoordinator
{
    Task<bool> TryExecuteCombatAsync(
        ISessionContext context,
        PlayerSnapshot player,
        WorldSnapshot world,
        CombatOptions options,
        string logPrefix,
        IProgress<string>? progress,
        CancellationToken cancellationToken);

    void Reset();

    void BlacklistTarget(int targetId, DateTime now);
}
