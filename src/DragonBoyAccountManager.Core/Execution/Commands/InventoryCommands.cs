namespace DragonBoyAccountManager.Core.Execution.Commands;

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
