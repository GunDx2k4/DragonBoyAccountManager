using System;
using System.Linq;
using System.Windows;
using DragonBoyAccountManager.GUI.ViewModels;
using DragonBoyAccountManager.GUI.Views;

namespace DragonBoyAccountManager.GUI.Presentation.Shell.Dialogs;

public sealed class DialogService : IDialogService
{
    private static Window? GetActiveWindow()
    {
        return System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
               ?? System.Windows.Application.Current?.MainWindow;
    }

    public void ShowInfo(string title, string message)
    {
        RunOnUi(() =>
        {
            var vm = new GameDialogViewModel
            {
                Title = title,
                Message = message,
                Severity = DialogSeverity.Info,
                PrimaryButtonText = "Đóng",
                IsConfirmation = false
            };
            var win = new GameDialogWindow(vm) { Owner = GetActiveWindow() };
            win.ShowDialog();
        });
    }

    public void ShowWarning(string title, string message)
    {
        RunOnUi(() =>
        {
            var vm = new GameDialogViewModel
            {
                Title = title,
                Message = message,
                Severity = DialogSeverity.Warning,
                PrimaryButtonText = "Đóng",
                IsConfirmation = false
            };
            var win = new GameDialogWindow(vm) { Owner = GetActiveWindow() };
            win.ShowDialog();
        });
    }

    public void ShowError(string title, string message)
    {
        RunOnUi(() =>
        {
            var vm = new GameDialogViewModel
            {
                Title = title,
                Message = message,
                Severity = DialogSeverity.Error,
                PrimaryButtonText = "Đóng",
                IsConfirmation = false
            };
            var win = new GameDialogWindow(vm) { Owner = GetActiveWindow() };
            win.ShowDialog();
        });
    }

    public bool ShowConfirmation(string title, string message, string yesText = "Đồng ý", string noText = "Hủy")
    {
        return RunOnUi(() =>
        {
            var vm = new GameDialogViewModel
            {
                Title = title,
                Message = message,
                Severity = DialogSeverity.Confirm,
                PrimaryButtonText = "Đóng",
                SecondaryButtonText = noText,
                IsConfirmation = true
            };
            var win = new GameDialogWindow(vm) { Owner = GetActiveWindow() };
            return win.ShowDialog() == true;
        });
    }

    public string? ShowOpenFileDialog(string title, string filter, string? initialDirectory = null)
    {
        return RunOnUi(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                InitialDirectory = string.IsNullOrWhiteSpace(initialDirectory) ? string.Empty : initialDirectory
            };
            return dialog.ShowDialog(GetActiveWindow()) == true ? dialog.FileName : null;
        });
    }

    public void ShowHotkeyCheatsheet(Window? owner = null)
    {
        RunOnUi(() =>
        {
            var win = new HotkeyCheatsheetWindow
            {
                Owner = owner ?? GetActiveWindow()
            };
            win.ShowDialog();
        });
    }

    private static void RunOnUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            app.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    private static T RunOnUi<T>(Func<T> func)
    {
        var app = System.Windows.Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            return app.Dispatcher.Invoke(func);
        }
        return func();
    }
}
