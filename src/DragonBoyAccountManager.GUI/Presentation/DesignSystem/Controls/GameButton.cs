using System.Windows;
using System.Windows.Controls;

namespace DragonBoyAccountManager.GUI.Presentation.DesignSystem.Controls;

public class GameButton : Button
{
    static GameButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GameButton),
            new FrameworkPropertyMetadata(typeof(GameButton)));
    }

    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(
            nameof(Variant),
            typeof(GameButtonVariant),
            typeof(GameButton),
            new PropertyMetadata(GameButtonVariant.Secondary));

    public GameButtonVariant Variant
    {
        get => (GameButtonVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(GameButton),
            new PropertyMetadata(new CornerRadius(5)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(GameButton),
            new PropertyMetadata(null));

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
