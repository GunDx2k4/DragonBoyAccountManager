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

public sealed record MoveCommand(
    int TargetX,
    int TargetY,
    bool IsFastTeleport = false
) : IGameCommand
{
    public string Name => $"Di chuyển tới ({TargetX}, {TargetY})";
}

public sealed record UseItemCommand(
    sbyte BagIndex,
    short TemplateId = -1,
    string ItemName = "Vật phẩm"
) : IGameCommand
{
    public string Name => $"Dùng {ItemName} (ô {BagIndex})";
}

public sealed record PickItemCommand(
    int ItemMapId,
    string ItemName = "Vật phẩm"
) : IGameCommand
{
    public string Name => $"Nhặt {ItemName} #{ItemMapId}";
}

public sealed record SelectSkillCommand(
    int SlotIndex
) : IGameCommand
{
    public string Name => $"Chọn kỹ năng ô {SlotIndex + 1}";
}

public sealed record SendChatCommand(
    string Text,
    bool IsGlobal = false
) : IGameCommand
{
    public string Name => $"Chat: {Text}";
}

public sealed record WakeUpCommand : IGameCommand
{
    public string Name => "Hồi sinh về nhà";
}

public sealed record ChangeZoneCommand(
    int ZoneId
) : IGameCommand
{
    public string Name => $"Đổi sang khu {ZoneId}";
}

public sealed record OpenZonePanelCommand : IGameCommand
{
    public string Name => "Mở bảng chọn khu vực";
}

public sealed record CloseZonePanelCommand : IGameCommand
{
    public string Name => "Đóng bảng chọn khu vực";
}

public sealed record AssignSkillsCommand : IGameCommand
{
    public string Name => "Tự động gán kỹ năng vào phím tắt";
}

public sealed record LoginCommand(
    string Username,
    string Password,
    string Version
) : IGameCommand
{
    public string Name => $"Đăng nhập {Username}";
}

public sealed record FocusEntityCommand(
    string EntityType,
    int EntityId,
    string EntityName = ""
) : IGameCommand
{
    public string Name => string.IsNullOrWhiteSpace(EntityName)
        ? $"Chọn {EntityType} #{EntityId}"
        : $"Chọn {EntityName}";
}
