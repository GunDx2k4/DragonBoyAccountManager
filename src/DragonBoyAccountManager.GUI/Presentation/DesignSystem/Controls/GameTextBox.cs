using System.Windows;
using System.Windows.Controls;

namespace DragonBoyAccountManager.GUI.Presentation.DesignSystem.Controls;

public class GameTextBox : TextBox
{
    static GameTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(GameTextBox),
            new FrameworkPropertyMetadata(typeof(GameTextBox)));
    }

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.Register(
            nameof(Placeholder),
            typeof(string),
            typeof(GameTextBox),
            new PropertyMetadata(string.Empty));

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(GameTextBox),
            new PropertyMetadata(new CornerRadius(4)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty LeadingIconProperty =
        DependencyProperty.Register(
            nameof(LeadingIcon),
            typeof(object),
            typeof(GameTextBox),
            new PropertyMetadata(null));

    public object? LeadingIcon
    {
        get => GetValue(LeadingIconProperty);
        set => SetValue(LeadingIconProperty, value);
    }
}
