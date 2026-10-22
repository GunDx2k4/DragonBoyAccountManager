using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Services.Engine;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;

public sealed partial class SkillButtonItem : ObservableObject
{
    public int SlotIndex { get; }

    public string DefaultName => SlotIndex == 9 ? "Skill 0" : $"Skill {SlotIndex + 1}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private string _name;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _iconId = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    private ImageSource? _icon;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private int _point;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private int _coolDown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToolTipText))]
    private int _manaUse;

    [ObservableProperty]
    private long _lastTimeUse;

    [ObservableProperty]
    private bool _isOnCooldown;

    [ObservableProperty]
    private double _cooldownProgress;

    [ObservableProperty]
    private double _cooldownOverlayHeight;

    [ObservableProperty]
    private string _cooldownText = string.Empty;

    public bool HasIcon => Icon != null;

    public string ShortcutBadge => SlotIndex switch
    {
        9 => "0",
        _ => $"{SlotIndex + 1}"
    };

    public string ToolTipText =>
        $"[{ShortcutBadge}] {Name}" +
        (Point > 0 ? $"\nCấp độ: {Point}" : "") +
        (CoolDown > 0 ? $"\nThời gian hồi: {CoolDown / 1000.0:0.#}s" : "") +
        (ManaUse > 0 ? $"\nTiêu hao: {ManaUse} KI" : "");

    public SkillButtonItem(int slotIndex, string name, bool isSelected = false, int iconId = -1, ImageSource? icon = null, long lastTimeUse = 0)
    {
        SlotIndex = slotIndex;
        _name = name;
        _isSelected = isSelected;
        _iconId = iconId;
        _icon = icon;
        _lastTimeUse = lastTimeUse;
    }

    public void SyncFrom(GameSkillInfo? info, IGameImageService imageService, bool syncSelection)
    {
        if (info != null)
        {
            string expectedName = !string.IsNullOrWhiteSpace(info.Name) ? info.Name : DefaultName;
            if (Name != expectedName) Name = expectedName;
            if (syncSelection && IsSelected != info.IsSelected) IsSelected = info.IsSelected;

            Point = info.Point;
            CoolDown = info.CoolDown;
            ManaUse = info.ManaUse;
            LastTimeUse = info.LastTimeUse;

            if (info.IconId >= 0 && IconId != info.IconId)
            {
                IconId = info.IconId;
                Icon = imageService.GetSmallImage(info.IconId);
            }
        }
        else if (syncSelection)
        {
            Reset(true);
        }
    }

    public void Reset(bool resetSelection = true)
    {
        if (Name != DefaultName) Name = DefaultName;
        if (resetSelection && IsSelected) IsSelected = false;
        Point = 0;
        CoolDown = 0;
        ManaUse = 0;
        LastTimeUse = 0;
        IconId = -1;
        Icon = null;
    }

    public void UpdateCooldown(long now)
    {
        if (CoolDown <= 0 || LastTimeUse <= 0)
        {
            if (IsOnCooldown)
            {
                IsOnCooldown = false;
                CooldownProgress = 0;
                CooldownOverlayHeight = 0;
                CooldownText = string.Empty;
            }
            return;
        }

        long elapsed = now - LastTimeUse;
        if (elapsed < CoolDown)
        {
            IsOnCooldown = true;
            double remaining = CoolDown - elapsed;
            double progress = Math.Clamp(remaining / CoolDown, 0.0, 1.0);
            CooldownProgress = progress;
            CooldownOverlayHeight = progress * 28.0;
            CooldownText = remaining >= 1000 ? $"{remaining / 1000.0:0.#}s" : $"{remaining / 1000.0:0.0}s";
        }
        else
        {
            if (IsOnCooldown)
            {
                IsOnCooldown = false;
                CooldownProgress = 0;
                CooldownOverlayHeight = 0;
                CooldownText = string.Empty;
            }
        }
    }
}
