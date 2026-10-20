using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.GUI.Messaging;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AppTitleBarViewModel : ObservableObject
{
    private readonly GuideViewModel _guide;

    public bool HasAppUpdate => _guide.HasAppUpdate;
    public string AppVersion => _guide.AppVersion;

    public AppTitleBarViewModel(GuideViewModel guide)
    {
        _guide = guide;
        _guide.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(GuideViewModel.HasAppUpdate) or nameof(GuideViewModel.AppVersion))
            {
                OnPropertyChanged(e.PropertyName);
            }
        };
    }

    [RelayCommand]
    private void NavigateToGuide()
    {
        WeakReferenceMessenger.Default.Send(new RequestNavigateToTabMessage(4));
    }
}
