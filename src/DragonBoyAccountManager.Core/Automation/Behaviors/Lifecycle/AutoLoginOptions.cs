namespace DragonBoyAccountManager.Core.Automation.Behaviors.Lifecycle;

public sealed class AutoLoginOptions
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public int ServerId { get; init; }
    public string? ServerName { get; init; }
    public int MaxAttempts { get; init; } = 10;
    public bool IsMasked { get; init; }
}
