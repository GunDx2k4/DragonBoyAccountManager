using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Presentation.Shell.Dialogs;

public sealed partial class GameDialogViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTitle))]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isConfirmation;

    [ObservableProperty]
    private string _primaryButtonText = "Đóng";

    [ObservableProperty]
    private string? _secondaryButtonText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HeaderBrushKey))]
    private DialogSeverity _severity = DialogSeverity.Info;

    public bool HasTitle => !string.IsNullOrWhiteSpace(Title) && !string.Equals(Title, "Thông báo", StringComparison.OrdinalIgnoreCase);

    public string HeaderBrushKey => Severity switch
    {
        DialogSeverity.Error => "GameTextRedBrush",
        DialogSeverity.Warning => "GameTextYellowBrush",
        DialogSeverity.Confirm => "GameHeaderBrush",
        _ => "GameTextGreenBrush"
    };

    public bool? DialogResult { get; set; }
}
