using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragonBoyAccountManager.GUI.Services.GameEngine;
using DragonBoyAccountManager.GUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DragonBoyAccountManager.GUI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Don't trigger shortcuts while user is typing in any text box or password box
        var focused = Keyboard.FocusedElement;
        if (focused is TextBox or PasswordBox or RichTextBox)
            return;
        if (e.OriginalSource is TextBox or PasswordBox or RichTextBox)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        if (e.Key == Key.C)
        {
            vm.UtilitiesTab.UseCapsuleCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.X)
        {
            vm.UtilitiesTab.UseTdltCommand.Execute(null);
            e.Handled = true;
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        try
        {
            var gameEngine = App.Services?.GetService<IGameEngineService>();
            gameEngine?.StopAll();
        }
        catch { }
    }

    private void UtilitiesTabView_Loaded(object sender)
    {

    }
}
