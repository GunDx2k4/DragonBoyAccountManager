namespace DragonBoyAccountManager.Core.Execution.Commands;

public sealed record AttackMobCommand(
    int MobId,
    string MobName = "Quái",
    int TargetX = 0,
    int SkillSlot = 0
) : IGameCommand
{
    public string Name => $"Tấn công {MobName} #{MobId}";
}

public sealed record SelectSkillCommand(
    int SlotIndex
) : IGameCommand
{
    public string Name => $"Chọn kỹ năng ô {SlotIndex + 1}";
}

public sealed record SetInGameAutoTrainCommand(
    bool Enable,
    string Reason = "DBAM",
    bool AvoidSuperMobs = true,
    bool ChangeZoneWhenNoMobs = false,
    bool CheckAttackByHp = false,
    long MinMobHp = 0,
    long MaxMobHp = 10000000001L,
    bool AutoUsePean = true,
    int PeanHpPercent = 50,
    int PeanMpPercent = 50,
    bool CheckAllMobs = true,
    IReadOnlyList<string>? TargetMobNames = null,
    IReadOnlyList<int>? SelectedSkillSlots = null,
    IReadOnlyList<short>? SelectedItemTemplateIds = null,
    IReadOnlyList<short>? SelectedBuaTemplateIds = null,
    bool AutoPick = true,
    bool PickOthersItems = false,
    bool PickAllMap = false,
    bool AutoDropItems = false,
    string FilterPickIds = "",
    string FilterExcludeIds = "",
    string FilterDropIds = ""
) : IGameCommand
{
    public string Name => Enable ? "Bật tự đánh trong game" : "Tắt tự đánh trong game";
}

public sealed record AssignSkillsCommand : IGameCommand
{
    public string Name => "Tự động gán kỹ năng vào phím tắt";
}
