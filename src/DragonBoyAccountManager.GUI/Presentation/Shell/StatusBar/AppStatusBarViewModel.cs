using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Sessions.Validation;
using DragonBoyAccountManager.GUI.Services.Dispatching;

namespace DragonBoyAccountManager.GUI.Presentation.Shell.StatusBar;

public sealed partial class AppStatusBarViewModel : ObservableObject
{
    private readonly IGameEnvironmentService _envService;
    private readonly IDialogService _dialogService;
    private readonly IDispatcherService? _dispatcher;

    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng";

    [ObservableProperty]
    private string _appVersion;

    public string GamePath
    {
        get => _envService.GamePath;
        set
        {
            if (_envService.GamePath != value)
            {
                _envService.GamePath = value;
                OnPropertyChanged();
                GamePathChanged?.Invoke(value);
            }
        }
    }

    public ClientStatus Status => _envService.Status;
    public string AssemblyStatus => _envService.AssemblyStatus;
    public string AssemblyStatusTooltip => _envService.AssemblyStatusTooltip;
    public string AssemblyPath => _envService.AssemblyPath;
    public string GameVersion => _envService.GameVersion;

    public event Action<string>? GamePathChanged;

    public AppStatusBarViewModel(
        IGameEnvironmentService envService,
        IDialogService dialogService,
        IDispatcherService? dispatcher = null)
    {
        _envService = envService;
        _dialogService = dialogService;
        _dispatcher = dispatcher;
        _appVersion = AppUpdaterService.CurrentVersionString;

        WeakReferenceMessenger.Default.Register<StatusMessageChangedMessage>(this, (_, m) =>
        {
            _dispatcher.Dispatch(() => StatusMessage = m.Message);
        });

        _envService.EnvironmentChanged += () => _dispatcher.Dispatch(() =>
        {
            OnPropertyChanged(nameof(GamePath));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(AssemblyStatus));
            OnPropertyChanged(nameof(AssemblyStatusTooltip));
            OnPropertyChanged(nameof(AssemblyPath));
            OnPropertyChanged(nameof(GameVersion));
        });
    }

    public void CheckAssemblyStatus()
    {
        _envService.CheckStatus();
    }

    [RelayCommand]
    private void BrowseGamePath()
    {
        if (_envService.BrowseAndSelectGamePath(_dialogService))
        {
            StatusMessage = $"Đã chọn game: {Path.GetFileName(GamePath)}";
        }
    }

    [RelayCommand]
    public void OpenAssemblyFolder()
    {
        if (!string.IsNullOrWhiteSpace(AssemblyPath) && File.Exists(AssemblyPath))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{AssemblyPath}\"");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Lỗi", $"Không thể mở thư mục: {ex.Message}");
            }
        }
        else if (!string.IsNullOrWhiteSpace(GamePath) && File.Exists(GamePath))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{GamePath}\"");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Lỗi", $"Không thể mở thư mục: {ex.Message}");
            }
        }
        else
        {
            bool chooseNow = _dialogService.ShowConfirmation(
                "Chưa chọn game",
                "Chưa chọn file thực thi game Dragonboy252.exe.\n\n" +
                "Vui lòng tải bản game PC gốc 2.5.2 (Dragonboy252.exe) tại trang chủ (ngocrongonline.com) — tuyệt đối không cài bản mod.\n\n" +
                "Bạn có muốn chọn file game (.exe) ngay bây giờ không?",
                "Chọn file ngay",
                "Đóng");
            if (chooseNow)
            {
                BrowseGamePath();
            }
        }
    }
}
