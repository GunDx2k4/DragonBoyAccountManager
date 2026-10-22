using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DragonBoyAccountManager.Core.Sessions.Validation;
using DragonBoyAccountManager.GUI.Services.Dialogs;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.Services.Engine;

public sealed class GameEnvironmentService : IGameEnvironmentService
{
    private readonly ISettingsService _settingsService;
    private readonly IGameClientValidator _clientValidator;
    private Dictionary<string, string>? _versionChecksums;

    public string GamePath
    {
        get => _settingsService.Current.GamePath ?? string.Empty;
        set
        {
            if (_settingsService.Current.GamePath != value)
            {
                _settingsService.Current.GamePath = value;
                _ = _settingsService.SaveAsync();
                CheckStatus();
            }
        }
    }

    public ClientStatus Status { get; private set; } = ClientStatus.GameNotFound;
    public string AssemblyStatus { get; private set; } = "Chưa chọn file game .exe";
    public string AssemblyStatusTooltip { get; private set; } = "Chưa cài đặt đường dẫn game hợp lệ.";
    public string AssemblyPath { get; private set; } = string.Empty;
    public string GameVersion { get; private set; } = string.Empty;
    public bool IsAssemblyMissing => Status is ClientStatus.GameNotFound or ClientStatus.AssemblyNotFound;
    public ClientMetadata? CurrentMetadata { get; private set; }

    public event Action? EnvironmentChanged;

    public GameEnvironmentService(ISettingsService settingsService, IGameClientValidator clientValidator)
    {
        _settingsService = settingsService;
        _clientValidator = clientValidator;

        CheckStatus();
        _ = LoadChecksumsAndVerifyAsync();
    }

    public bool BrowseAndSelectGamePath(IDialogService dialogService)
    {
        var initialDir = !string.IsNullOrEmpty(GamePath)
            ? Path.GetDirectoryName(GamePath)
            : null;

        var selectedFile = dialogService.ShowOpenFileDialog(
            "Chọn tệp thực thi game Dragon Boy",
            "Game Executable (*.exe)|*.exe|Tất cả tệp (*.*)|*.*",
            initialDir);

        if (string.IsNullOrEmpty(selectedFile))
            return false;

        if (!string.Equals(Path.GetExtension(selectedFile), ".exe", StringComparison.OrdinalIgnoreCase))
        {
            dialogService.ShowWarning("File không hợp lệ", "Đường dẫn game bắt buộc phải là file thực thi (.exe)!");
            return false;
        }

        GamePath = selectedFile;
        return true;
    }

    public async Task LoadChecksumsAndVerifyAsync()
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

        CheckStatus();
    }

    public void CheckStatus()
    {
        string path = GamePath;
        var metadata = _clientValidator.InspectClientMetadata(path, _versionChecksums);
        CurrentMetadata = metadata;

        GameVersion = metadata.DetectedVersion ?? metadata.TargetVersion;
        AssemblyPath = metadata.AssemblyPath ?? string.Empty;
        Status = metadata.Status;

        switch (metadata.Status)
        {
            case ClientStatus.OfficialVersionMatch:
                AssemblyStatus = $"v{metadata.DetectedVersion} (Khớp bản gốc)";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} byte\n" +
                                         $"Phiên bản: v{metadata.DetectedVersion} (Bản gốc chính thức)\n" +
                                         $"Trạng thái: Khớp bản gốc\n" +
                                         $"(Bấm để mở thư mục chứa tệp)";
                break;

            case ClientStatus.OutdatedVersion:
                AssemblyStatus = $"Cần cập nhật v{metadata.TargetVersion}";
                string oldVerState = metadata.IsShaMatch ? "Bản gốc cũ" : "Bản mod cũ";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} byte\n" +
                                         $"Phiên bản tệp: v{metadata.DetectedVersion}\n" +
                                         $"Phiên bản mới nhất: v{metadata.TargetVersion}\n" +
                                         $"Trạng thái: Cần cập nhật ({oldVerState})\n" +
                                         $"(Bấm để mở thư mục chứa tệp)";
                break;

            case ClientStatus.ModifiedOrMod:
                AssemblyStatus = metadata.IsCorrectVersion
                    ? $"v{metadata.DetectedVersion} (Không khớp - Bản mod)"
                    : "Không khớp (Bản mod)";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} byte\n" +
                                         $"Phiên bản phát hiện: v{metadata.DetectedVersion ?? "Không rõ"}\n" +
                                         $"Phiên bản chính thức: v{metadata.TargetVersion}\n" +
                                         $"Trạng thái: Không khớp với bản gốc (Bản mod hoặc đã can thiệp)\n" +
                                         $"(Bấm để mở thư mục chứa tệp)";
                break;

            case ClientStatus.AssemblyNotFound:
                AssemblyStatus = "Không tìm thấy Assembly";
                AssemblyStatusTooltip = $"Đã nhận file game: {Path.GetFileName(path)}\nNhưng không tìm thấy file Assembly-CSharp.dll trong thư mục dữ liệu Data\\Managed.\n(Bấm để chọn lại file game hoặc mở thư mục)";
                break;

            case ClientStatus.Error:
                AssemblyStatus = "Lỗi đọc file";
                AssemblyStatusTooltip = $"Lỗi đọc file: {metadata.ErrorMessage}";
                break;

            case ClientStatus.GameNotFound:
            default:
                AssemblyStatus = "Chưa chọn file game .exe";
                AssemblyStatusTooltip = "Chưa cài đặt đường dẫn game hợp lệ. Bấm vào nút này để chọn file thực thi (.exe) của game.";
                break;
        }

        EnvironmentChanged?.Invoke();
    }
}
