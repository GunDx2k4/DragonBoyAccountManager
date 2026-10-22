using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto.Items;

public sealed partial class MobFilterItem : ObservableObject
{
    public int TemplateId { get; }
    public int? MobId { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullDisplayTitle))]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullDisplayTitle))]
    [NotifyPropertyChangedFor(nameof(HpDisplay))]
    private long _hp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullDisplayTitle))]
    [NotifyPropertyChangedFor(nameof(HpDisplay))]
    private long _maxHp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FlyStatusText))]
    private bool _isFlying;

    [ObservableProperty]
    private bool _isSuperMob;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullDisplayTitle))]
    [NotifyPropertyChangedFor(nameof(HasMultipleCount))]
    [NotifyPropertyChangedFor(nameof(CountDisplay))]
    private int _count;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCustom;

    public string FullDisplayTitle
    {
        get
        {
            string idPart = MobId.HasValue ? $" #{MobId.Value}" : string.Empty;
            string hpPart = MaxHp > 0 ? $" [{Hp:N0}/{MaxHp:N0}]" : (Hp > 0 ? $" [{Hp:N0}]" : string.Empty);
            string countPart = (!MobId.HasValue && Count > 1) ? $" (x{Count})" : string.Empty;
            return $"{Name}{idPart}{hpPart}{countPart}";
        }
    }

    public string HpDisplay => MaxHp > 0 ? $"{Hp:N0}/{MaxHp:N0}" : (Hp > 0 ? $"{Hp:N0}" : "-");

    public string FlyStatusText => IsFlying ? "Bay" : "Đất";

    public bool HasMultipleCount => !MobId.HasValue && Count > 1;

    public string CountDisplay => $"x{Count}";

    public MobFilterItem(
        int templateId,
        string name,
        long hp = 0,
        long maxHp = 0,
        bool isFlying = false,
        bool isSuperMob = false,
        int count = 1,
        bool isSelected = true,
        bool isCustom = false,
        int? mobId = null)
    {
        TemplateId = templateId;
        _name = name;
        _hp = hp;
        _maxHp = maxHp;
        _isFlying = isFlying;
        _isSuperMob = isSuperMob;
        _count = count;
        _isSelected = isSelected;
        _isCustom = isCustom;
        MobId = mobId;
    }
}
