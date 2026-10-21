using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;

namespace DragonBoyAccountManager.Core.Engine.Models;

public record AccountLaunchOptions
{
    public required Guid AccountId { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public int ServerId { get; init; }
    public string ServerName { get; init; } = "Vũ trụ 1";
    public required string GamePath { get; init; }
    public int ScreenWidth { get; init; } = 1024;
    public int ScreenHeight { get; init; } = 600;
    public float GameSpeed { get; init; } = 1.0f;
    public bool AutoLogin { get; init; } = true;
    public bool AutoAssignSkills { get; init; }
    public AutoTrainOptions? TrainOptions { get; init; }
    public AutoChatOptions? ChatOptions { get; init; }
}
