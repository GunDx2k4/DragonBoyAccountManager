namespace DragonBoyAccountManager.Core.Execution.Commands;

public sealed record WakeUpCommand : IGameCommand
{
    public string Name => "Hồi sinh về nhà";
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
