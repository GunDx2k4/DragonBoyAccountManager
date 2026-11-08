namespace DragonBoyAccountManager.Core.Execution.Commands;

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
