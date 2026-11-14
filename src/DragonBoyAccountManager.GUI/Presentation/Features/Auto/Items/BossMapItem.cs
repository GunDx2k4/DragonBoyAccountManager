using CommunityToolkit.Mvvm.ComponentModel;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;

public sealed partial class BossMapItem : ObservableObject
{
    private readonly Action? _onSelectionChanged;

    public int MapId { get; }
    public string MapName { get; }
    public int PlanetId { get; }
    public string PlanetName { get; }
    public string DisplayText => $"[{MapId}] {MapName} ({PlanetName})";

    [ObservableProperty]
    private bool _isSelected;

    public BossMapItem(int mapId, string mapName, int planetId, Action? onSelectionChanged = null)
    {
        MapId = mapId;
        MapName = mapName;
        PlanetId = planetId;
        PlanetName = PlanetNames.GetName(planetId);
        _onSelectionChanged = onSelectionChanged;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _onSelectionChanged?.Invoke();
    }
}
