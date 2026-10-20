using DragonBoyAccountManager.Core.Game.Modules;

namespace DragonBoyAccountManager.Core.Game.Routines.Chat;

public sealed class AutoChatOptions
{
    public IReadOnlyList<string> Messages { get; init; } = [];
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(5);
    public ChatType ChatType { get; init; } = ChatType.Map;
    public bool IsRandom { get; init; }
}
