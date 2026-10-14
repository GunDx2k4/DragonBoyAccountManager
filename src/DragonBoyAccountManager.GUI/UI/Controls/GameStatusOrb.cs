using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.UI.Controls;

public class GameStatusOrb : Control
{
    static GameStatusOrb()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GameStatusOrb),
            new FrameworkPropertyMetadata(typeof(GameStatusOrb)));
    }

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(
            nameof(Status),
            typeof(object),
            typeof(GameStatusOrb),
            new PropertyMetadata(null));

    public object? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly DependencyProperty OrbBrushProperty =
        DependencyProperty.Register(
            nameof(OrbBrush),
            typeof(Brush),
            typeof(GameStatusOrb),
            new PropertyMetadata(null));

    public Brush? OrbBrush
    {
        get => (Brush?)GetValue(OrbBrushProperty);
        set => SetValue(OrbBrushProperty, value);
    }

    public static readonly DependencyProperty SizeProperty =
        DependencyProperty.Register(
            nameof(Size),
            typeof(double),
            typeof(GameStatusOrb),
            new PropertyMetadata(12.0));

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }
}
