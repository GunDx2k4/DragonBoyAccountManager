namespace DragonBoyAccountManager.GUI.Domain.Settings;

public sealed class WindowArrangeOptions
{
    public WindowArrangeMode Mode { get; set; } = WindowArrangeMode.Grid;
    public int Columns { get; set; }
    public int SpacingX { get; set; }
    public int SpacingY { get; set; }
}
