using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTrainViewModel
{
    public ObservableCollection<MobFilterItem> AvailableMobTypes { get; } = [];

    private void ClearAvailableMobTypes()
    {
        for (int i = 0; i < AvailableMobTypes.Count; i++)
        {
            AvailableMobTypes[i].PropertyChanged -= OnMobItemPropertyChanged;
        }
        AvailableMobTypes.Clear();
    }

    [RelayCommand]
    public void SelectAllMobs()
    {
        RunBulkUpdate(() =>
        {
            for (int i = 0; i < AvailableMobTypes.Count; i++)
            {
                AvailableMobTypes[i].IsSelected = true;
            }
        });
        OnOptionChanged();
    }

    [RelayCommand]
    public void DeselectAllMobs()
    {
        RunBulkUpdate(() =>
        {
            for (int i = 0; i < AvailableMobTypes.Count; i++)
            {
                AvailableMobTypes[i].IsSelected = false;
            }
        });
        OnOptionChanged();
    }

    private void UpdateAvailableMobTypes(IReadOnlyList<WorldMobInfo> mobs)
    {
        var savedMobNames = ActiveAccount?.AutoSettings.GetBehaviorOptionsOrDefault<AutoTrainOptions>(BehaviorId).TargetMobNames ?? [];
        var savedSet = savedMobNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var distinctNames = mobs
            .Select(m => m.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingMap = AvailableMobTypes.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);
        var currentNames = distinctNames.ToHashSet(StringComparer.OrdinalIgnoreCase);

        RunBulkUpdate(() =>
        {
            for (int i = AvailableMobTypes.Count - 1; i >= 0; i--)
            {
                var item = AvailableMobTypes[i];
                if (!currentNames.Contains(item.Name))
                {
                    item.PropertyChanged -= OnMobItemPropertyChanged;
                    AvailableMobTypes.RemoveAt(i);
                }
            }

            for (int i = 0; i < distinctNames.Count; i++)
            {
                string name = distinctNames[i];
                if (!existingMap.ContainsKey(name))
                {
                    bool isSelected = savedSet.Contains(name);
                    var mobItem = new MobFilterItem(
                        templateId: 0,
                        name: name,
                        isSelected: isSelected);
                    mobItem.PropertyChanged += OnMobItemPropertyChanged;
                    AvailableMobTypes.Add(mobItem);
                }
            }
        });
    }

    private void OnMobItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MobFilterItem.IsSelected))
        {
            OnOptionChanged();
        }
    }
}
