using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Presentation.Features.MapUtilities.Dialogs;

public sealed partial class XmapGraphEdgeViewModel : ObservableObject
{
    public int FromId { get; init; }
    public int ToId { get; init; }
    public double X1 { get; init; }
    public double Y1 { get; init; }
    public double X2 { get; init; }
    public double Y2 { get; init; }
    public bool IsNpc { get; init; }

    [ObservableProperty]
    private bool _isOnRoute;
}
