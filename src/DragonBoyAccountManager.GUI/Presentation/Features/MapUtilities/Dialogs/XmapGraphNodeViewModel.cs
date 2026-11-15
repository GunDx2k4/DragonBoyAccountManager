using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Dialogs;

public sealed partial class XmapGraphNodeViewModel : ObservableObject
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PlanetName { get; init; } = string.Empty;
    public int PlanetIndex { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; init; } = 120;
    public double Height { get; init; } = 48;

    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    [ObservableProperty]
    private bool _isCurrentMap;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isOnRoute;

    [ObservableProperty]
    private bool _isHighlighted;

    public int ConnectionCount { get; init; }
    public string DisplayText => $"#{Id} {Name}";
}
