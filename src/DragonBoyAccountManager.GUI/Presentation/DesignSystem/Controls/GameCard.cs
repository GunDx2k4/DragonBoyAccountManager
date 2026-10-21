using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Presentation.DesignSystem.Controls;

public class GameCard : HeaderedContentControl
{
    static GameCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GameCard),
            new FrameworkPropertyMetadata(typeof(GameCard)));
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(GameCard),
            new PropertyMetadata(new CornerRadius(7)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty HighlightBrushProperty =
        DependencyProperty.Register(
            nameof(HighlightBrush),
            typeof(Brush),
            typeof(GameCard),
            new PropertyMetadata(null));

    public Brush? HighlightBrush
    {
        get => (Brush?)GetValue(HighlightBrushProperty);
        set => SetValue(HighlightBrushProperty, value);
    }

    public static readonly DependencyProperty ShadowBrushProperty =
        DependencyProperty.Register(
            nameof(ShadowBrush),
            typeof(Brush),
            typeof(GameCard),
            new PropertyMetadata(null));

    public Brush? ShadowBrush
    {
        get => (Brush?)GetValue(ShadowBrushProperty);
        set => SetValue(ShadowBrushProperty, value);
    }

    public static readonly DependencyProperty HeaderActionProperty =
        DependencyProperty.Register(
            nameof(HeaderAction),
            typeof(object),
            typeof(GameCard),
            new PropertyMetadata(null));

    public object? HeaderAction
    {
        get => GetValue(HeaderActionProperty);
        set => SetValue(HeaderActionProperty, value);
    }
}
