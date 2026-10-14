using System.Windows;

using DragonBoyAccountManager.GUI.Views;

namespace DragonBoyAccountManager.GUI.Services.Dialog;

public sealed class DialogService : IDialogService
{
    private static Window? GetActiveWindow()
    {
        return Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) 
               ?? Application.Current?.MainWindow;
    }

    public void ShowInfo(string title, string message)
    {
        RunOnUi(() =>
        {
            GameDialogWindow.ShowAlert(GetActiveWindow(), title, message, buttonText: "Đóng", headerColor: "#2E7D32");
        });
    }

    public void ShowWarning(string title, string message)
    {
        RunOnUi(() =>
        {
            GameDialogWindow.ShowAlert(GetActiveWindow(), title, message, buttonText: "Đóng", headerColor: "#B45309");
        });
    }

    public void ShowError(string title, string message)
    {
        RunOnUi(() =>
        {
            GameDialogWindow.ShowAlert(GetActiveWindow(), title, message, buttonText: "Đóng", headerColor: "#C62828");
        });
    }

    public bool ShowConfirmation(string title, string message)
    {
        return RunOnUi(() =>
        {
            return GameDialogWindow.ShowConfirm(GetActiveWindow(), title, message, yesText: "Đồng ý", noText: "Hủy");
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

    private static void RunOnUi(Action action)
    {
        var app = Application.Current;
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
        var app = Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
        {
            return app.Dispatcher.Invoke(func);
        }
        return func();
    }
}
