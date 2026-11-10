namespace DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

public sealed record AutoTrainOptions
{
    public IReadOnlyList<int> SelectedSkillSlots { get; init; } = [0];
    public IReadOnlyList<short> SelectedItemTemplateIds { get; init; } = [];
    public IReadOnlyList<short> SelectedBuaTemplateIds { get; init; } = [];
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
    public bool AutoPick { get; init; } = true;
    public bool PickOthersItems { get; init; } = false;
    public bool PickAllMap { get; init; } = false;
    public bool AutoDropItems { get; init; } = false;
    public string FilterPickIds { get; init; } = string.Empty;
    public string FilterExcludeIds { get; init; } = string.Empty;
    public string FilterDropIds { get; init; } = string.Empty;
}
