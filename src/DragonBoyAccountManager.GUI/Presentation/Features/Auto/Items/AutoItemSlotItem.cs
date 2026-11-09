using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DragonBoyAccountManager.GUI.Services;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;

public sealed partial class AutoItemSlotItem : ObservableObject
{
    public int Index { get; }
    public short TemplateId { get; }
    public int IconId { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private ImageSource? _icon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasQuantity))]
    [NotifyPropertyChangedFor(nameof(QuantityDisplay))]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private int _quantity;

    public AutoItemSlotItem(int index, short templateId, int iconId, string name)
    {
        Index = index;
        TemplateId = templateId;
        IconId = iconId;
        Name = name;
    }

    public bool HasIcon => Icon is not null;

    public bool HasQuantity => Quantity > 0;

    public string QuantityDisplay => Quantity > 0 ? $"x{Quantity}" : "0";

    public string ToolTipText => Quantity > 0
        ? $"{Name} (ID: {TemplateId}) - Hành trang: x{Quantity}"
        : $"{Name} (ID: {TemplateId}) - Chưa có trong hành trang";

    public void EnsureIconLoaded(IGameImageService imageService)
    {
        if (Icon is null && IconId > 0)
        {
            Icon = imageService.GetSmallImage(IconId);
        }
    }
}
