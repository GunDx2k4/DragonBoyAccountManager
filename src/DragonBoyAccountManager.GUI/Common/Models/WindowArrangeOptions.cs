using DragonBoyAccountManager.GUI.Common.Enums;

namespace DragonBoyAccountManager.GUI.Common.Models;

public sealed class WindowArrangeOptions
{
    public WindowArrangeMode Mode { get; set; } = WindowArrangeMode.Grid;
    public int Columns { get; set; }
    public int SpacingX { get; set; }
    public int SpacingY { get; set; }
}
