using System.ComponentModel;
using System.Windows;
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
