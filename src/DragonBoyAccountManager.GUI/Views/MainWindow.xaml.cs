using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.GameEngine;
using DragonBoyAccountManager.GUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DragonBoyAccountManager.GUI.Views;

public partial class MainWindow : Window
{
    private readonly HashSet<Key> _heldMovementKeys = [];

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

        GameKey? movementKey = e.Key switch
        {
            Key.Up or Key.W => GameKey.Up,
            Key.Down or Key.S => GameKey.Down,
            Key.Left or Key.A => GameKey.Left,
            Key.Right or Key.D => GameKey.Right,
            _ => null
        };

        if (movementKey.HasValue)
        {
            if (!_heldMovementKeys.Contains(e.Key))
            {
                _heldMovementKeys.Add(e.Key);
                vm.AccountForm.StartHoldingKey(movementKey.Value);
            }
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter or Key.Space)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionOkAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C)
        {
            vm.AccountForm.UseCapsule();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.X)
        {
            vm.AccountForm.UseTdlt();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.D1 or Key.NumPad1)
        {
            vm.AccountForm.UseSkillCommand.Execute(0);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.D2 or Key.NumPad2)
        {
            vm.AccountForm.UseSkillCommand.Execute(1);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.D3 or Key.NumPad3)
        {
            vm.AccountForm.UseSkillCommand.Execute(2);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.D4 or Key.NumPad4)
        {
            vm.AccountForm.UseSkillCommand.Execute(3);
            e.Handled = true;
            return;
        }

        if (e.Key is Key.D5 or Key.NumPad5)
        {
            vm.AccountForm.UseSkillCommand.Execute(4);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemOpenBrackets) // '['
        {
            vm.AccountForm.PreviousZoneCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemCloseBrackets) // ']'
        {
            vm.AccountForm.NextZoneCommand.Execute(null);
            e.Handled = true;
            return;
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement;
        if (focused is TextBox or PasswordBox or RichTextBox)
            return;
        if (e.OriginalSource is TextBox or PasswordBox or RichTextBox)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        GameKey? movementKey = e.Key switch
        {
            Key.Up or Key.W => GameKey.Up,
            Key.Down or Key.S => GameKey.Down,
            Key.Left or Key.A => GameKey.Left,
            Key.Right or Key.D => GameKey.Right,
            _ => null
        };

        if (movementKey.HasValue)
        {
            _heldMovementKeys.Remove(e.Key);
            vm.AccountForm.StopHoldingKey(movementKey.Value);
            e.Handled = true;
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        ReleaseAllHeldKeys();
    }

    private void ReleaseAllHeldKeys()
    {
        if (_heldMovementKeys.Count == 0) return;
        if (DataContext is MainViewModel vm)
        {
            foreach (var key in _heldMovementKeys.ToList())
            {
                GameKey? movementKey = key switch
                {
                    Key.Up or Key.W => GameKey.Up,
                    Key.Down or Key.S => GameKey.Down,
                    Key.Left or Key.A => GameKey.Left,
                    Key.Right or Key.D => GameKey.Right,
                    _ => null
                };
                if (movementKey.HasValue)
                {
                    vm.AccountForm.StopHoldingKey(movementKey.Value);
                }
            }
        }
        _heldMovementKeys.Clear();
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
