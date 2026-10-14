using System.Windows;
using System.Windows.Controls;

namespace DragonBoyAccountManager.GUI.UI.Controls;

public class GameBadge : ContentControl
{
    static GameBadge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GameBadge),
            new FrameworkPropertyMetadata(typeof(GameBadge)));
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(GameBadge),
            new PropertyMetadata(new CornerRadius(4)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }
}
