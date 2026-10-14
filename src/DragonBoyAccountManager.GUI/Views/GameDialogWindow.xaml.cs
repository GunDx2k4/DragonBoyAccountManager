using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DragonBoyAccountManager.GUI.Views;

public partial class GameDialogWindow : Window
{
    public GameDialogWindow()
    {
        InitializeComponent();
    }

    public static void ShowAlert(Window? owner, string title, string message, string buttonText = "Đóng", string? headerColor = null)
    {
        var dlg = new GameDialogWindow();
        dlg.Configure(owner, title, message, isConfirmation: false, primaryText: buttonText, secondaryText: null, headerColor: headerColor);
        dlg.ShowDialog();
    }

    public static bool ShowConfirm(Window? owner, string title, string message, string yesText = "Đồng ý", string noText = "Hủy")
    {
        var dlg = new GameDialogWindow();
        dlg.Configure(owner, title, message, isConfirmation: true, primaryText: yesText, secondaryText: noText, headerColor: "#8B2500");
        return dlg.ShowDialog() == true;
    }

    private void Configure(Window? owner, string title, string message, bool isConfirmation, string primaryText, string? secondaryText, string? headerColor)
    {
        if (owner != null && owner.IsLoaded)
        {
            Owner = owner;
        }
        else if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsLoaded && Application.Current.MainWindow != this)
        {
            Owner = Application.Current.MainWindow;
        }

        MessageTextBlock.Text = message;

        if (!string.IsNullOrWhiteSpace(title) && !string.Equals(title, "Thông báo", StringComparison.OrdinalIgnoreCase))
        {
            TitleTextBlock.Text = title.ToUpperInvariant();
            TitleTextBlock.Visibility = Visibility.Visible;
            if (!string.IsNullOrEmpty(headerColor))
            {
                try
                {
                    TitleTextBlock.Foreground = (Brush)new BrushConverter().ConvertFromString(headerColor)!;
                }
                catch { }
            }
        }
        else
        {
            TitleTextBlock.Visibility = Visibility.Collapsed;
        }

        PrimaryButton.Content = primaryText;

        if (isConfirmation)
        {
            SecondaryButton.Content = secondaryText ?? "Hủy";
            SecondaryButton.Visibility = Visibility.Visible;
        }
        else
        {
            SecondaryButton.Visibility = Visibility.Collapsed;
        }
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
