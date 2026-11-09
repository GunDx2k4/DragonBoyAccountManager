namespace DragonBoyAccountManager.Core.Sessions;

public sealed record GameConfigOptions
{
    public bool IsPlaySound { get; init; } = true;
    public bool LowGraphic { get; init; } = false;
    public bool IsPaintAura { get; init; } = true;
    public bool IsPaintAura2 { get; init; } = true;
    public bool IsPaintChatVip { get; init; } = true;
    public int ZoomLevel { get; init; } = 2;
    public bool EnablePickTab { get; init; } = true;
}
