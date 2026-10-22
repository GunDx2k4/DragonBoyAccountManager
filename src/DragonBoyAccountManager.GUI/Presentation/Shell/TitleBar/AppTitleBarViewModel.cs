using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace DragonBoyAccountManager.GUI.Presentation.Shell.TitleBar;

public sealed partial class AppTitleBarViewModel : ObservableObject
{
    private readonly IAppUpdaterService _updater;
    private readonly IDispatcherService? _dispatcher;

    public bool HasAppUpdate => _updater.LatestUpdate != null;
    public string AppVersion { get; }
    public string UpdateTooltip => _updater.LatestUpdate != null
        ? $"Có bản cập nhật mới v{_updater.LatestUpdate.Version}! Bấm vào tab Hướng dẫn để xem chi tiết."
        : "Không có bản cập nhật mới.";

    public AppTitleBarViewModel(IAppUpdaterService updater, IDispatcherService? dispatcher = null)
    {
        _updater = updater;
        _dispatcher = dispatcher;
        AppVersion = _updater.CurrentVersion;

        _updater.UpdateDetected += _ =>
        {
            RunOnUi(() =>
            {
                OnPropertyChanged(nameof(HasAppUpdate));
                OnPropertyChanged(nameof(UpdateTooltip));
            });
        };
    }

    private void RunOnUi(Action action)
    {
        if (_dispatcher != null)
        {
            _ = _dispatcher.InvokeAsync(action);
        }
        else if (App.Current?.Dispatcher != null)
        {
            _ = App.Current.Dispatcher.InvokeAsync(action);
        }
        else
        {
            action();
        }
    }

    [RelayCommand]
    private void NavigateToGuide()
    {
        WeakReferenceMessenger.Default.Send(new RequestNavigateToTabMessage(AppTab.Guide));
    }
}
