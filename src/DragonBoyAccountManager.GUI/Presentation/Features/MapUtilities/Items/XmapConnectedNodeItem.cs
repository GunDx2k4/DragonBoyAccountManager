using CommunityToolkit.Mvvm.ComponentModel;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Items;

public sealed partial class XmapConnectedNodeItem : ObservableObject
{
    public int FromMapId { get; init; }
    public int TargetMapId { get; init; }
    public string TargetMapName { get; init; } = string.Empty;
    public string TransitionType { get; init; } = "Cổng";
    public MapDirection Direction { get; init; }
    public string DirectionLabel { get; init; } = string.Empty;
    public string CoordinateDisplay { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isOnRoute;

    public bool HasCoordinates => !string.IsNullOrWhiteSpace(CoordinateDisplay);
    public string DisplayText => $"#{TargetMapId} {TargetMapName}";
    public bool IsNpc => TransitionType == "NPC" || Direction == MapDirection.Npc;
}
