using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Presentation.DesignSystem.Controls;

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
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnStatusChanged));

    public object? Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly DependencyProperty StatusKeyProperty =
        DependencyProperty.Register(
            nameof(StatusKey),
            typeof(string),
            typeof(GameStatusOrb),
            new FrameworkPropertyMetadata("Offline", FrameworkPropertyMetadataOptions.AffectsRender));

    public string StatusKey
    {
        get => (string)GetValue(StatusKeyProperty);
        private set => SetValue(StatusKeyProperty, value);
    }

    public static readonly DependencyProperty OrbBrushProperty =
        DependencyProperty.Register(
            nameof(OrbBrush),
            typeof(Brush),
            typeof(GameStatusOrb),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

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
            new FrameworkPropertyMetadata(14.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    private static void OnStatusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GameStatusOrb orb)
        {
            orb.UpdateStatusKey();
        }
    }

    private void UpdateStatusKey()
    {
        if (Status == null)
        {
            StatusKey = "Offline";
            return;
        }

        string raw = Status.ToString() ?? "Offline";
        StatusKey = raw switch
        {
            "Active" or "OfficialVersionMatch" => "Active",
            "Running" => "Running",
            "LoggingIn" => "LoggingIn",
            "Waiting" => "Waiting",
            "Offline" or "GameNotFound" or "AssemblyNotFound" => "Offline",
            "Banned" or "Error" or "OutdatedVersion" => "Error",
            "ModifiedOrMod" => "Warning",
            _ => raw
        };
    }
}
