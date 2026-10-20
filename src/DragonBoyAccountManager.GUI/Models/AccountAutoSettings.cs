namespace DragonBoyAccountManager.GUI.Models;

public sealed class AccountAutoSettings
{
    public bool IgnoreBosses { get; set; } = true;
    public bool AvoidSuperMobs { get; set; } = true;
    public bool AutoPickItems { get; set; } = true;
    public bool AutoUsePean { get; set; } = true;
    public bool UseTdlt { get; set; } = true;
    public int MinHpPercent { get; set; } = 20;
    public int MinMpPercent { get; set; } = 20;
    public List<int> SelectedSkillSlots { get; set; } = [0];
    public List<int> TargetMobTemplateIds { get; set; } = [];
    public List<int> TargetMobIds { get; set; } = [];
    public int MobTargetModeIndex { get; set; } = 0;

    public string ChatText { get; set; } = "Chào mọi người!";
    public int ChatDelaySeconds { get; set; } = 5;
    public int SelectedChatTypeIndex { get; set; } = 0;
    public bool IsRandomChat { get; set; } = false;
}
