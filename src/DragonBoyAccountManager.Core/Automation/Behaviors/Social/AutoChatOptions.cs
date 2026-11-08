namespace DragonBoyAccountManager.Core.Automation.Behaviors.Social;

public sealed record AutoChatOptions
{
    public IReadOnlyList<string> Messages { get; init; } = ["Chào mọi người!"];
    public TimeSpan Delay { get; init; } = TimeSpan.FromSeconds(5);
    public bool IsGlobal { get; init; }
    public bool IsRandom { get; init; }
}
