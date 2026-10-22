using System.Windows;
using System.Windows.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Shell.Dialogs;

public partial class HotkeyCheatsheetWindow : Window
{
    public HotkeyCheatsheetWindow()
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key is Key.Escape or Key.F12)
        {
            Close();
            e.Handled = true;
        }
    }
}
