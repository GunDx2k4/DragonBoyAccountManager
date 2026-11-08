namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed record AutoTrainOptions
{
    public IReadOnlyList<int> SelectedSkillSlots { get; init; } = [0];
    public IReadOnlyList<string> TargetMobNames { get; init; } = [];
    public bool CheckAllMobs { get; init; } = true;
    public bool AvoidSuperMobs { get; init; } = true;
    public bool ChangeZoneWhenNoMobs { get; init; } = false;
    public bool CheckAttackByHp { get; init; } = false;
    public long MinMobHp { get; init; } = 0;
    public long MaxMobHp { get; init; } = 10000000001L;
    public bool AutoUsePean { get; init; } = true;
    public int MinHpPercent { get; init; } = 50;
    public int MinMpPercent { get; init; } = 50;
}
