using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Models;

public sealed partial class SkillButtonItem : ObservableObject
{
    public int SlotIndex { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private string _name;

    [ObservableProperty]
    private bool _isSelected;

    public string ShortcutBadge => SlotIndex switch
    {
        9 => "[0]",
        _ => $"[{SlotIndex + 1}]"
    };

    public string ToolTipText => SlotIndex switch
    {
        9 => $"Sử dụng {Name} (Phím 0)",
        _ => $"Sử dụng {Name} (Phím {SlotIndex + 1})"
    };

    public SkillButtonItem(int slotIndex, string name, bool isSelected = false)
    {
        SlotIndex = slotIndex;
        _name = name;
        _isSelected = isSelected;
    }
}
