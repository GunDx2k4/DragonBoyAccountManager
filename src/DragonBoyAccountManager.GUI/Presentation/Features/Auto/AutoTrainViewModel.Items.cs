using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTrainViewModel
{
    public ObservableCollection<AutoItemSlotItem> AutoTrainItems { get; } = [];
    public ObservableCollection<AutoItemSlotItem> AutoTrainBuas { get; } = [];

    private void InitializeItems()
    {
        for (int i = 0; i < GameDefinitions.AutoTrainItems.Count; i++)
        {
            var def = GameDefinitions.AutoTrainItems[i];
            var autoItem = new AutoItemSlotItem(def.Index, def.TemplateId, def.IconId, def.Name);
            autoItem.PropertyChanged += OnAutoItemPropertyChanged;
            AutoTrainItems.Add(autoItem);
        }

        for (int i = 0; i < GameDefinitions.AutoTrainBuas.Count; i++)
        {
            var def = GameDefinitions.AutoTrainBuas[i];
            var autoBua = new AutoItemSlotItem(def.Index, def.TemplateId, def.IconId, def.Name);
            autoBua.PropertyChanged += OnAutoBuaPropertyChanged;
            AutoTrainBuas.Add(autoBua);
        }
    }

    [RelayCommand]
    public void SelectAllItems()
    {
        RunBulkUpdate(() =>
        {
            for (int i = 0; i < AutoTrainItems.Count; i++)
            {
                AutoTrainItems[i].IsSelected = true;
            }
        });
        UpdateItemSelectionStatusAndSave();
    }

    [RelayCommand]
    public void DeselectAllItems()
    {
        RunBulkUpdate(() =>
        {
            for (int i = 0; i < AutoTrainItems.Count; i++)
            {
                AutoTrainItems[i].IsSelected = false;
            }
        });
        UpdateItemSelectionStatusAndSave();
    }

    private void RefreshAutoItems(IReadOnlyList<InventoryItem> bagItems)
    {
        var countByTemplate = new Dictionary<short, int>();
        for (int i = 0; i < bagItems.Count; i++)
        {
            var b = bagItems[i];
            int qty = Math.Max(1, b.Quantity);
            if (countByTemplate.TryGetValue(b.TemplateId, out int existing))
            {
                countByTemplate[b.TemplateId] = existing + qty;
            }
            else
            {
                countByTemplate[b.TemplateId] = qty;
            }
        }

        for (int i = 0; i < AutoTrainItems.Count; i++)
        {
            var item = AutoTrainItems[i];
            item.EnsureIconLoaded(_gameImageService);
            item.Quantity = countByTemplate.GetValueOrDefault(item.TemplateId, 0);
        }
    }

    private void OnAutoItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AutoItemSlotItem.IsSelected))
        {
            UpdateItemSelectionStatusAndSave();
        }
    }

    private void OnAutoBuaPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AutoItemSlotItem.IsSelected))
        {
            OnOptionChanged();
        }
    }

    private void UpdateItemStatusText()
    {
        int selectedCount = 0;
        for (int i = 0; i < AutoTrainItems.Count; i++)
        {
            if (AutoTrainItems[i].IsSelected) selectedCount++;
        }
        for (int i = 0; i < AutoTrainBuas.Count; i++)
        {
            if (AutoTrainBuas[i].IsSelected) selectedCount++;
        }

        ItemStatusText = selectedCount > 0
            ? $"Đã chọn {selectedCount} vật phẩm / bùa"
            : "Chưa chọn vật phẩm tự dùng";
    }

    private void UpdateItemSelectionStatusAndSave()
    {
        bool hasAnySelected = false;
        for (int i = 0; i < AutoTrainItems.Count; i++)
        {
            if (AutoTrainItems[i].IsSelected)
            {
                hasAnySelected = true;
                break;
            }
        }
        HasSelectedAutoItems = hasAnySelected;
        UpdateItemStatusText();
        OnOptionChanged();
    }
}
