namespace DragonBoyAccountManager.Core.Automation.Behaviors.Boss;

public sealed record AutoBossOptions
{
    public IReadOnlyList<int> SelectedMapIds { get; init; } = [];
    public string TargetBossName { get; init; } = "Zombie";
    public int MinZone { get; init; } = 2;
    public int ZoneChangeDelaySeconds { get; init; } = 11;
    public bool IsFastTeleport { get; init; } = true;
    public bool PickOthersItems { get; init; } = false;
    public string FilterPickNames { get; init; } = string.Empty;
    public IReadOnlyList<int> SelectedSkillSlots { get; init; } = [];
}
