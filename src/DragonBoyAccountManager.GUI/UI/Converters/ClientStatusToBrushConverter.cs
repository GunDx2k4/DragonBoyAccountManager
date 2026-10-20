using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;


namespace DragonBoyAccountManager.GUI.UI.Converters;

public sealed class ClientStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GreenText = Freeze(new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32)));
    private static readonly SolidColorBrush GreenBg = Freeze(new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9)));
    private static readonly SolidColorBrush RedText = Freeze(new SolidColorBrush(Color.FromRgb(0xC6, 0x28, 0x28)));
    private static readonly SolidColorBrush RedBg = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEE)));
    private static readonly SolidColorBrush MutedText = Freeze(new SolidColorBrush(Color.FromRgb(0x54, 0x6E, 0x7A)));
    private static readonly SolidColorBrush MutedBg = Freeze(new SolidColorBrush(Color.FromRgb(0xEC, 0xEF, 0xF1)));

    private static SolidColorBrush Freeze(SolidColorBrush b)
    {
        b.Freeze();
        return b;
    }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isBg = string.Equals(parameter as string, "Background", StringComparison.OrdinalIgnoreCase);

        if (value is ClientStatus status)
        {
            return status switch
            {
                ClientStatus.OfficialVersionMatch => isBg ? GreenBg : GreenText,
                ClientStatus.OutdatedVersion or ClientStatus.ModifiedOrMod or ClientStatus.Error => isBg ? RedBg : RedText,
                _ => isBg ? MutedBg : MutedText
            };
        }

        return isBg ? MutedBg : MutedText;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
