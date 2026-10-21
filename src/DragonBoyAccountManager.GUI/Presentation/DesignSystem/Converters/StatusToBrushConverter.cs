using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Presentation.DesignSystem.Converters;

public class StatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush ActiveTextBrush = CreateFrozenBrush(0, 122, 7);
    private static readonly SolidColorBrush ActiveBgBrush = CreateFrozenBrush(250, 215, 160);

    private static readonly SolidColorBrush RunningTextBrush = CreateFrozenBrush(179, 95, 0);
    private static readonly SolidColorBrush RunningBgBrush = CreateFrozenBrush(250, 215, 160);

    private static readonly SolidColorBrush LoggingInTextBrush = CreateFrozenBrush(0, 102, 204);
    private static readonly SolidColorBrush WaitingTextBrush = CreateFrozenBrush(120, 80, 160);

    private static readonly SolidColorBrush OfflineTextBrush = CreateFrozenBrush(90, 90, 90);
    private static readonly SolidColorBrush OfflineBgBrush = CreateFrozenBrush(250, 215, 160);

    private static readonly SolidColorBrush BannedTextBrush = CreateFrozenBrush(198, 40, 40);
    private static readonly SolidColorBrush BannedBgBrush = CreateFrozenBrush(250, 215, 160);

    private static readonly SolidColorBrush ErrorTextBrush = CreateFrozenBrush(198, 40, 40);
    private static readonly SolidColorBrush ErrorBgBrush = CreateFrozenBrush(250, 215, 160);

    private static readonly SolidColorBrush DefaultTextBrush = CreateFrozenBrush(74, 40, 0);
    private static readonly SolidColorBrush DefaultBgBrush = CreateFrozenBrush(250, 215, 160);

    private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b, byte a = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var statusStr = value?.ToString() ?? string.Empty;
        var isBackground = parameter?.ToString()?.Equals("Background", StringComparison.OrdinalIgnoreCase) == true;

        return statusStr switch
        {
            "Active" => isBackground ? ActiveBgBrush : ActiveTextBrush,
            "Running" => isBackground ? RunningBgBrush : RunningTextBrush,
            "LoggingIn" => isBackground ? RunningBgBrush : LoggingInTextBrush,
            "Waiting" => isBackground ? RunningBgBrush : WaitingTextBrush,
            "Offline" => isBackground ? OfflineBgBrush : OfflineTextBrush,
            "Banned" => isBackground ? BannedBgBrush : BannedTextBrush,
            "Error" => isBackground ? ErrorBgBrush : ErrorTextBrush,
            _ => isBackground ? DefaultBgBrush : DefaultTextBrush
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
