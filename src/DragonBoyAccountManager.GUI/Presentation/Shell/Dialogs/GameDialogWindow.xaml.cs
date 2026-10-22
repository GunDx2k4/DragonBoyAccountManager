using System.Windows;
using System.Windows.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Shell.Dialogs;

public partial class GameDialogWindow : Window
{
    public GameDialogWindow()
    {
        InitializeComponent();
    }

    public GameDialogWindow(GameDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    public GameDialogViewModel? ViewModel => DataContext as GameDialogViewModel;

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.DialogResult = true;
        }
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            ViewModel.DialogResult = false;
        }
        DialogResult = false;
        Close();
    }
}
