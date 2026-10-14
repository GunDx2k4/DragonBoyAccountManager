using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Models;

public sealed partial class SkillButtonItem : ObservableObject
{
    public int SlotIndex { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private string _name;

    public string ShortcutBadge => $"[{SlotIndex + 1}]";

    public string ToolTipText => $"Sử dụng {Name} (Phím {SlotIndex + 1})";

    public SkillButtonItem(int slotIndex, string name)
    {
        SlotIndex = slotIndex;
        _name = name;
    }
}
