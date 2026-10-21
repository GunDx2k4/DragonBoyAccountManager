using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AppTitleBarViewModel : ObservableObject
{
    private readonly IAppUpdaterService _updater;

    public bool HasAppUpdate => _updater.LatestUpdate != null;
    public string AppVersion { get; }
    public string UpdateTooltip => _updater.LatestUpdate != null
        ? $"Có bản cập nhật mới v{_updater.LatestUpdate.Version}! Bấm vào tab Hướng dẫn để xem chi tiết."
        : "Không có bản cập nhật mới.";

    public AppTitleBarViewModel(IAppUpdaterService updater)
    {
        _updater = updater;
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        AppVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        _updater.UpdateDetected += _ =>
        {
            OnPropertyChanged(nameof(HasAppUpdate));
            OnPropertyChanged(nameof(UpdateTooltip));
        };
    }

    [RelayCommand]
    private void NavigateToGuide()
    {
        WeakReferenceMessenger.Default.Send(new RequestNavigateToTabMessage(4));
    }
}
