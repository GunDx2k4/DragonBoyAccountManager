using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Engine.Models;

public sealed record AccountLaunchOptions
{
    public Guid AccountId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public int ServerId { get; init; } = -1;
    public string ServerName { get; init; } = "Vũ trụ 1";
    public required string GamePath { get; init; }
    public string? WorkingDirectory { get; init; }
    public string? AdditionalArguments { get; init; }
    public int ScreenWidth { get; init; } = 1024;
    public int ScreenHeight { get; init; } = 600;
    public int ZoomLevel { get; init; } = 2;
    public bool EnableLogging { get; init; } = true;
    public string? LogPrefix { get; init; }
    public bool AutoLogin { get; init; } = true;
    public bool AutoReconnect { get; init; } = true;
    public int ReconnectDelaySeconds { get; init; } = 3;
    public Func<(bool AutoReconnect, int DelaySeconds)>? ReconnectPolicyResolver { get; init; }
    public bool AutoAssignSkills { get; init; }
    public bool IsMasked { get; init; }
    public GameConfigOptions? InitialGameConfig { get; init; }
    public Action<GameSession>? ConfigureSession { get; init; }
}
