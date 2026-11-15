using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Items;

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
