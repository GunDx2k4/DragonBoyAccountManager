using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Items;

public sealed record WaypointButtonItem(string Name, int X, int Y, WorldWaypointInfo Waypoint)
{
    public string DisplayCoordinates => $"({X}, {Y})";

    public string ToolTipText => $"Chuyển tới {Name} ({X}, {Y})";
}
