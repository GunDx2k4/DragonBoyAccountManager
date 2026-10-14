using System.Diagnostics;
using System.IO;
using System.Reflection;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DragonBoyAccountManager.Core.Game;
using DragonBoyAccountManager.GUI.Services.Dialog;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AppStatusBarViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IGameClientValidator _clientValidator;

    [ObservableProperty]
    private string _gamePath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Sẵn sàng";

    [ObservableProperty]
    private string _appVersion;

    [ObservableProperty]
    private string _assemblyStatus = "Chưa chọn file game .exe";

    [ObservableProperty]
    private string _assemblyStatusColor = "#546E7A";

    [ObservableProperty]
    private string _assemblyStatusBg = "#ECEFF1";

    [ObservableProperty]
    private string _assemblyStatusTooltip = "Chưa cài đặt đường dẫn game hợp lệ.";

    [ObservableProperty]
    private string _assemblyPath = string.Empty;

    [ObservableProperty]
    private string _gameVersion = string.Empty;

    private Dictionary<string, string>? _versionChecksums;

    public event Action<string>? GamePathChanged;

    public AppStatusBarViewModel(
        ISettingsService settingsService,
        IDialogService dialogService,
        IGameClientValidator clientValidator)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _clientValidator = clientValidator;

        _gamePath = settingsService.Current.GamePath ?? string.Empty;
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        _appVersion = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";

        CheckAssemblyStatus();
        _ = LoadSha256ChecksumsAsync();
    }

    public async Task LoadSha256ChecksumsAsync()
    {
        try
        {
            var dict = await _clientValidator.GetSha256ChecksumsAsync();
            if (dict != null && dict.Count > 0)
            {
                _versionChecksums = dict;
            }
        }
        catch { }

        CheckAssemblyStatus();
    }

    public void CheckAssemblyStatus()
    {
        string path = GamePath;
        var metadata = _clientValidator.InspectClientMetadata(path, _versionChecksums);

        GameVersion = metadata.DetectedVersion ?? metadata.TargetVersion;
        AssemblyPath = metadata.AssemblyPath ?? string.Empty;

        switch (metadata.Status)
        {
            case ClientStatus.OfficialVersionMatch:
                AssemblyStatus = $"v{metadata.DetectedVersion} (Bản gốc)";
                AssemblyStatusColor = "#2E7D32";
                AssemblyStatusBg = "#E8F5E9";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} byte\n" +
                                         $"Phiên bản: v{metadata.DetectedVersion} (Bản gốc chính thức)\n" +
                                         $"Mã SHA256: {metadata.ActualSha256}\n" +
                                         $"(Bấm để mở thư mục chứa tệp)";
                break;

            case ClientStatus.OutdatedVersion:
                AssemblyStatus = $"Cần cập nhật v{metadata.TargetVersion}";
                AssemblyStatusColor = "#C62828";
                AssemblyStatusBg = "#FFEBEE";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} byte\n" +
                                         $"Phiên bản tệp: v{metadata.DetectedVersion}\n" +
                                         $"Phiên bản mới nhất: v{metadata.TargetVersion}\n" +
                                         $"Trạng thái: Game chưa được cập nhật lên bản mới nhất!\n" +
                                         $"Mã SHA256: {metadata.ActualSha256}\n" +
                                         $"(Bấm để mở thư mục chứa tệp)";
                break;

            case ClientStatus.ModifiedOrMod:
                AssemblyStatus = "Chưa đúng phiên bản (Bản mod)";
                AssemblyStatusColor = "#C62828";
                AssemblyStatusBg = "#FFEBEE";
                var expected = (_versionChecksums != null && _versionChecksums.TryGetValue(metadata.TargetVersion, out var exp))
                    ? $"\nMã SHA256 gốc v{metadata.TargetVersion}: {exp}"
                    : "";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} byte\n" +
                                         $"Phiên bản chính thức: v{metadata.TargetVersion}\n" +
                                         $"Trạng thái: Không khớp mã SHA256 gốc (Bản mod hoặc sai phiên bản){expected}\n" +
                                         $"Mã SHA256 thực tế: {metadata.ActualSha256}\n" +
                                         $"(Bấm để mở thư mục chứa tệp)";
                break;

            case ClientStatus.AssemblyNotFound:
                AssemblyStatus = "Không tìm thấy Assembly";
                AssemblyStatusColor = "#546E7A";
                AssemblyStatusBg = "#ECEFF1";
                AssemblyStatusTooltip = $"Đã nhận file game: {Path.GetFileName(path)}\nNhưng không tìm thấy file Assembly-CSharp.dll trong thư mục dữ liệu Data\\Managed.";
                break;

            case ClientStatus.Error:
                AssemblyStatus = "Lỗi đọc file";
                AssemblyStatusColor = "#C62828";
                AssemblyStatusBg = "#FFEBEE";
                AssemblyStatusTooltip = $"Lỗi đọc file: {metadata.ErrorMessage}";
                break;

            case ClientStatus.GameNotFound:
            default:
                AssemblyStatus = "Chưa chọn file game .exe";
                AssemblyStatusColor = "#546E7A";
                AssemblyStatusBg = "#ECEFF1";
                AssemblyStatusTooltip = "Đường dẫn game bắt buộc phải là file thực thi (.exe).\nVui lòng chọn file game (.exe) để kiểm tra phiên bản.";
                break;
        }

        if (metadata.IsFound && StatusMessage == "Sẵn sàng")
        {
            StatusMessage = AssemblyStatus;
        }
    }

    partial void OnGamePathChanged(string value)
    {
        _settingsService.Current.GamePath = value;
        _ = _settingsService.SaveAsync();
        CheckAssemblyStatus();
        GamePathChanged?.Invoke(value);
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
            _dialogService.ShowWarning("Thông báo", "Chưa tìm thấy file Assembly-CSharp.dll hoặc file game đã chọn!");
        }
    }
}
