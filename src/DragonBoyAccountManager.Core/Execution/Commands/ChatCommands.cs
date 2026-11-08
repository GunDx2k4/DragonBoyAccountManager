namespace DragonBoyAccountManager.Core.Execution.Commands;

public sealed record SendChatCommand(
    string Text,
    bool IsGlobal = false
) : IGameCommand
{
    public string Name => $"Chat: {Text}";
}
