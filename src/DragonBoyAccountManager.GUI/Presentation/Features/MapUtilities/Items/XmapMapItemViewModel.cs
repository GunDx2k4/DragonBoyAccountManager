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

public sealed class XmapRouteStepItem
{
    public int StepIndex { get; init; }
    public int MapId { get; init; }
    public string MapName { get; init; } = string.Empty;
    public string TransitionText { get; init; } = string.Empty;
    public string DirectionText { get; init; } = string.Empty;
    public bool IsLast { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed partial class XmapMapItemViewModel : ObservableObject, IEquatable<XmapMapItemViewModel>
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PlanetName { get; init; } = string.Empty;
    public int PlanetId { get; init; }
    public IReadOnlyList<XmapConnectedNodeItem> ConnectedNodes { get; init; } = [];

    [ObservableProperty]
    private bool _isCurrentMap;

    [ObservableProperty]
    private bool _isSelected;

    public string DisplayText
    {
        get => $"#{Id} {Name}";
        set { }
    }

    public string PlanetDisplay
    {
        get => string.IsNullOrWhiteSpace(PlanetName) ? "Chung" : PlanetName;
        set { }
    }

    public int ConnectionCount => ConnectedNodes.Count;

    public bool HasConnections => ConnectedNodes.Count > 0;

    public bool Equals(XmapMapItemViewModel? other) => other != null && Id == other.Id;
    public override bool Equals(object? obj) => obj is XmapMapItemViewModel other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
}
