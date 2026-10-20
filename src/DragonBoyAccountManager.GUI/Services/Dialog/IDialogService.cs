using System.Windows;

namespace DragonBoyAccountManager.GUI.Services.Dialog;

public interface IDialogService
{
    void ShowInfo(string title, string message);
    void ShowWarning(string title, string message);
    void ShowError(string title, string message);
    bool ShowConfirmation(string title, string message, string yesText = "Đồng ý", string noText = "Hủy");
    string? ShowOpenFileDialog(string title, string filter, string? initialDirectory = null);
    void ShowHotkeyCheatsheet(Window? owner = null);
}
