namespace DragonBoyAccountManager.Core.Game;

public record LaunchOptions
{
    public required string GamePath { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? AdditionalArguments { get; init; }
    public bool EnableLogging { get; init; } = true;
    public string LogPrefix { get; init; } = "DragonBoy";
}
