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

    public static void PopulateRouteSteps(
        ICollection<XmapRouteStepItem> target,
        int currentMapId,
        string currentMapName,
        IReadOnlyList<NavigationStep> route,
        string startTransitionText = "Bắt đầu")
    {
        target.Add(new XmapRouteStepItem
        {
            StepIndex = 1,
            MapId = currentMapId,
            MapName = currentMapName,
            TransitionText = startTransitionText,
            DirectionText = "Gốc",
            IsCurrent = true,
            IsLast = false
        });

        for (int i = 0; i < route.Count; i++)
        {
            var step = route[i];
            string dirText = step.Transition.Direction switch
            {
                MapDirection.Left => "Trái",
                MapDirection.Right => "Phải",
                MapDirection.Center => "Giữa",
                MapDirection.Npc => "NPC",
                _ => "Cổng"
            };

            target.Add(new XmapRouteStepItem
            {
                StepIndex = i + 2,
                MapId = step.TargetMapId,
                MapName = step.TargetMapName,
                TransitionText = step.Transition.IsNpc ? "NPC" : "Cổng",
                DirectionText = dirText,
                IsCurrent = false,
                IsLast = i == route.Count - 1
            });
        }
    }
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

    public string DisplayText => $"#{Id} {Name}";

    public string PlanetDisplay => string.IsNullOrWhiteSpace(PlanetName) ? "Chung" : PlanetName;

    public int ConnectionCount => ConnectedNodes.Count;

    public bool HasConnections => ConnectedNodes.Count > 0;

    public bool Equals(XmapMapItemViewModel? other) => other != null && Id == other.Id;
    public override bool Equals(object? obj) => obj is XmapMapItemViewModel other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
}
