namespace DragonBoyAccountManager.Core.Game;

public record LaunchOptions
{
    public required string GamePath { get; init; }
    public Guid? SessionId { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? AdditionalArguments { get; init; }
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }
    public bool EnableLogging { get; init; } = true;
    public string LogPrefix { get; init; } = "DragonBoy";
}
