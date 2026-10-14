using System.Windows;

namespace DragonBoyAccountManager.GUI.UI.Assists;

public static class ControlAssist
{
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.RegisterAttached(
            "CornerRadius",
            typeof(CornerRadius),
            typeof(ControlAssist),
            new FrameworkPropertyMetadata(new CornerRadius(4), FrameworkPropertyMetadataOptions.Inherits));

    public static void SetCornerRadius(DependencyObject element, CornerRadius value) => element.SetValue(CornerRadiusProperty, value);
    public static CornerRadius GetCornerRadius(DependencyObject element) => (CornerRadius)element.GetValue(CornerRadiusProperty);

    public static readonly DependencyProperty PlaceholderProperty =
        DependencyProperty.RegisterAttached(
            "Placeholder",
            typeof(string),
            typeof(ControlAssist),
            new FrameworkPropertyMetadata(string.Empty));

    public static void SetPlaceholder(DependencyObject element, string value) => element.SetValue(PlaceholderProperty, value);
    public static string GetPlaceholder(DependencyObject element) => (string)element.GetValue(PlaceholderProperty);

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached(
            "Icon",
            typeof(object),
            typeof(ControlAssist),
            new FrameworkPropertyMetadata(null));

    public static void SetIcon(DependencyObject element, object? value) => element.SetValue(IconProperty, value);
    public static object? GetIcon(DependencyObject element) => element.GetValue(IconProperty);
}
