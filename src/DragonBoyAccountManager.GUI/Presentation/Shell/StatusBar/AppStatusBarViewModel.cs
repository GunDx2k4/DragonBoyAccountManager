using System.Diagnostics;
using System.IO;
using System.Reflection;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DragonBoyAccountManager.Core.Sessions.Validation;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AppStatusBarViewModel : ObservableObject
{
    private readonly IGameEnvironmentService _envService;
    private readonly IDialogService _dialogService;

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
        IDialogService dialogService)
    {
        _envService = envService;
        _dialogService = dialogService;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _appVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        _envService.EnvironmentChanged += () =>
        {
            OnPropertyChanged(nameof(GamePath));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(AssemblyStatus));
            OnPropertyChanged(nameof(AssemblyStatusTooltip));
            OnPropertyChanged(nameof(AssemblyPath));
            OnPropertyChanged(nameof(GameVersion));
        };
    }

    public void CheckAssemblyStatus()
    {
        _envService.CheckStatus();
    }

    [RelayCommand]
    private void BrowseGamePath()
    {
        var initialDir = !string.IsNullOrEmpty(GamePath) ? Path.GetDirectoryName(GamePath) : null;
        var selectedFile = _dialogService.ShowOpenFileDialog(
            "Chọn tệp thực thi game Dragon Boy",
            "Game Executable (*.exe)|*.exe|Tất cả tệp (*.*)|*.*",
            initialDir);

        if (!string.IsNullOrEmpty(selectedFile))
        {
            if (!string.Equals(Path.GetExtension(selectedFile), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowWarning("File không hợp lệ", "Đường dẫn game bắt buộc phải là file thực thi (.exe)!");
                return;
            }

            GamePath = selectedFile;
            StatusMessage = $"Đã chọn game: {Path.GetFileName(selectedFile)}";
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
                "Chưa chọn game / Thiếu Assembly",
                "Chưa tìm thấy file Assembly-CSharp.dll hoặc chưa chọn file thực thi game (.exe).\n\n" +
                "Vui lòng tải bản game PC gốc 2.5.0 tại trang chủ (ngocrongonline.com) — tuyệt đối không cài bản mod.\n\n" +
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
