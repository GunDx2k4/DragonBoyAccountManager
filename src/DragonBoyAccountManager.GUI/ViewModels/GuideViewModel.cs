using System.Diagnostics;
using System.IO;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DragonBoyAccountManager.Core.Game;
using DragonBoyAccountManager.Core.Updater;
using DragonBoyAccountManager.GUI.Services.Dialog;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class GuideViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IGameClientValidator _clientValidator;
    private readonly IAppUpdaterService _updaterService;

    [ObservableProperty]
    private string _appVersion;

    [ObservableProperty]
    private ClientStatus _status = ClientStatus.GameNotFound;

    [ObservableProperty]
    private string _assemblyStatus = "Chưa chọn file game .exe";

    [ObservableProperty]
    private string _assemblyStatusColor = "#546E7A";

    [ObservableProperty]
    private string _assemblyStatusBg = "#ECEFF1";

    [ObservableProperty]
    private string _updateStatusMessage = "Vui lòng chọn đường dẫn game để kiểm tra phiên bản.";

    [ObservableProperty]
    private string _assemblyStatusTooltip = string.Empty;

    [ObservableProperty]
    private string _assemblyPath = string.Empty;

    [ObservableProperty]
    private string _assemblyHash = string.Empty;

    [ObservableProperty]
    private string _gameVersion = string.Empty;

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyUpdate))]
    private AppUpdateInfo? _availableAppUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyUpdate))]
    private bool _hasAppUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanApplyUpdate))]
    private bool _isUpdatingApp;

    [ObservableProperty]
    private double _updateProgress;

    [ObservableProperty]
    private string _appUpdateStatusText = string.Empty;

    public bool CanApplyUpdate => HasAppUpdate && !IsUpdatingApp;

    private Dictionary<string, string>? _versionChecksums;

    public GuideViewModel(
        ISettingsService settingsService,
        IDialogService dialogService,
        IGameClientValidator clientValidator,
        IAppUpdaterService updaterService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _clientValidator = clientValidator;
        _updaterService = updaterService;

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        _appVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        CheckAssemblyStatus();
        _ = LoadSha256ChecksumsAsync();
        _ = CheckAppUpdateSilentlyAsync();
    }

    public async Task CheckAppUpdateSilentlyAsync()
    {
        try
        {
            var update = await _updaterService.CheckForUpdatesAsync("GunDx2k4/DragonBoyAccountManager", null, "GUI");
            if (update != null)
            {
                AvailableAppUpdate = update;
                HasAppUpdate = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuideViewModel] Lỗi kiểm tra cập nhật ngầm: {ex.Message}");
        }
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GuideViewModel] Lỗi nạp checksums: {ex.Message}");
        }

        CheckAssemblyStatus();
    }

    public void CheckAssemblyStatus()
    {
        string gamePath = _settingsService.Current.GamePath;
        var metadata = _clientValidator.InspectClientMetadata(gamePath, _versionChecksums);

        Status = metadata.Status;
        GameVersion = metadata.DetectedVersion ?? metadata.TargetVersion;
        AssemblyPath = metadata.AssemblyPath ?? string.Empty;
        AssemblyHash = metadata.ActualSha256;

        switch (metadata.Status)
        {
            case ClientStatus.OfficialVersionMatch:
                AssemblyStatus = $"v{metadata.DetectedVersion} (Gốc - Khớp SHA)";
                AssemblyStatusColor = "#2E7D32";
                AssemblyStatusBg = "#E8F5E9";
                string shortHash = metadata.ActualSha256.Length >= 8 ? metadata.ActualSha256[..8] : metadata.ActualSha256;
                UpdateStatusMessage = $"Khớp phiên bản chính thức v{metadata.DetectedVersion} & SHA256 ({shortHash}...).";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} bytes\n" +
                                         $"Phiên bản: v{metadata.DetectedVersion} (Bản gốc chính thức)\n" +
                                         $"Mã SHA256: {metadata.ActualSha256}";
                break;

            case ClientStatus.OutdatedVersion:
                AssemblyStatus = $"Cần cập nhật v{metadata.TargetVersion}";
                AssemblyStatusColor = "#C62828";
                AssemblyStatusBg = "#FFEBEE";
                string shaOldInfo = metadata.IsShaMatch ? "Khớp SHA gốc bản cũ" : "Không khớp SHA bản cũ";
                UpdateStatusMessage = $"Game đang ở phiên bản cũ v{metadata.DetectedVersion} ({shaOldInfo}), bản mới nhất là v{metadata.TargetVersion}.";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} bytes\n" +
                                         $"Phiên bản tệp: v{metadata.DetectedVersion}\n" +
                                         $"Phiên bản mới nhất: v{metadata.TargetVersion}\n" +
                                         $"Trạng thái: {shaOldInfo}\n" +
                                         $"Mã SHA256 thực tế: {metadata.ActualSha256}\n" +
                                         (!string.IsNullOrEmpty(metadata.ExpectedSha256) ? $"Mã SHA256 mong muốn: {metadata.ExpectedSha256}" : "");
                break;

            case ClientStatus.ModifiedOrMod:
                if (metadata.IsCorrectVersion)
                {
                    AssemblyStatus = $"v{metadata.DetectedVersion} (Lệch SHA - Mod)";
                    AssemblyStatusColor = "#E65100";
                    AssemblyStatusBg = "#FFF3E0";
                    UpdateStatusMessage = $"Đúng phiên bản v{metadata.DetectedVersion} nhưng không khớp mã SHA256 gốc (File game đã bị can thiệp hoặc là bản mod).";
                }
                else
                {
                    AssemblyStatus = "Chưa đúng phiên bản (Bản mod)";
                    AssemblyStatusColor = "#C62828";
                    AssemblyStatusBg = "#FFEBEE";
                    UpdateStatusMessage = "Không khớp phiên bản và không khớp mã SHA256 gốc (File game đã bị can thiệp hoặc là bản mod).";
                }
                var expected = !string.IsNullOrEmpty(metadata.ExpectedSha256)
                    ? $"\nMã SHA256 gốc mong muốn: {metadata.ExpectedSha256}"
                    : "";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} bytes\n" +
                                         $"Phiên bản phát hiện: v{metadata.DetectedVersion ?? "Không xác định"}\n" +
                                         $"Phiên bản mục tiêu: v{metadata.TargetVersion}\n" +
                                         $"Mã SHA256 thực tế: {metadata.ActualSha256}{expected}";
                break;

            case ClientStatus.AssemblyNotFound:
                AssemblyStatus = "Không tìm thấy Assembly";
                AssemblyStatusColor = "#546E7A";
                AssemblyStatusBg = "#ECEFF1";
                UpdateStatusMessage = "Không tìm thấy file Assembly-CSharp.dll trong thư mục dữ liệu Data\\Managed.";
                AssemblyStatusTooltip = $"Đã nhận file game: {Path.GetFileName(gamePath)}\nNhưng không tìm thấy Assembly-CSharp.dll trong Data\\Managed.";
                break;

            case ClientStatus.Error:
                AssemblyStatus = "Lỗi đọc file";
                AssemblyStatusColor = "#C62828";
                AssemblyStatusBg = "#FFEBEE";
                UpdateStatusMessage = $"Lỗi: {metadata.ErrorMessage}";
                AssemblyStatusTooltip = metadata.ErrorMessage ?? "Lỗi đọc file";
                break;

            case ClientStatus.GameNotFound:
            default:
                AssemblyStatus = "Chưa chọn file game .exe";
                AssemblyStatusColor = "#546E7A";
                AssemblyStatusBg = "#ECEFF1";
                UpdateStatusMessage = "Đường dẫn game bắt buộc phải là file thực thi (.exe).\nVui lòng chọn file game ở thanh đáy cửa sổ.";
                AssemblyStatusTooltip = "Chưa cài đặt đường dẫn game hợp lệ.";
                break;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingUpdate = true;
        UpdateStatusMessage = "Đang kiểm tra phiên bản & xác thực SHA...";
        try
        {
            await Task.Delay(200);
            await LoadSha256ChecksumsAsync();

            // Kiểm tra cập nhật tool DBAM từ GitHub Releases (vừa check version vừa check SHA/commit)
            AppUpdateInfo? update = null;
            try
            {
                update = await _updaterService.CheckForUpdatesAsync("GunDx2k4/DragonBoyAccountManager", null, "GUI");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GuideViewModel] Lỗi kiểm tra cập nhật DBAM: {ex.Message}");
            }

            if (update != null)
            {
                AvailableAppUpdate = update;
                HasAppUpdate = true;

                string size = update.SizeBytes.HasValue ? $" ({update.SizeBytes.Value / 1024.0 / 1024.0:F1} MB)" : "";
                string notes = !string.IsNullOrWhiteSpace(update.ReleaseNotes) ? $"\n\nNội dung cập nhật:\n{update.ReleaseNotes}" : "";

                string shaDetails = "";
                if (!string.IsNullOrEmpty(update.RemoteSha256))
                {
                    shaDetails += $"\nMã SHA256 phát hành: {update.RemoteSha256}";
                }
                if (!string.IsNullOrEmpty(update.RemoteCommitSha))
                {
                    string localCommit = _updaterService.GetCurrentCommitSha();
                    string commitInfo = !string.IsNullOrEmpty(localCommit)
                        ? $"{localCommit[..Math.Min(7, localCommit.Length)]} -> {update.RemoteCommitSha[..Math.Min(7, update.RemoteCommitSha.Length)]}"
                        : update.RemoteCommitSha[..Math.Min(7, update.RemoteCommitSha.Length)];
                    shaDetails += $"\nCommit SHA: {commitInfo}";
                }

                string title = update.IsShaUpdateOnly ? "Cập nhật Tool DBAM (Hotfix/SHA)" : "Cập nhật Tool DBAM";
                string desc = update.IsShaUpdateOnly
                    ? $"Phát hiện bản cập nhật mới cùng phiên bản: v{update.Version}{size}\nPhiên bản hiện tại: v{AppVersion}{shaDetails}{notes}\n\nBạn có muốn cập nhật ngay bây giờ không?"
                    : $"Phát hiện phiên bản mới: v{update.Version}{size}\nPhiên bản hiện tại: v{AppVersion}{shaDetails}{notes}\n\nBạn có muốn cập nhật ngay bây giờ không?";

                bool confirm = _dialogService.ShowConfirmation(title, desc);

                if (confirm)
                {
                    await ApplyAppUpdateAsync();
                    return;
                }
            }
            else
            {
                HasAppUpdate = false;
                AvailableAppUpdate = null;
            }

            string localExeSha = _updaterService.GetCurrentExeSha256();
            string localCommitSha = _updaterService.GetCurrentCommitSha();
            string toolShaInfo = !string.IsNullOrEmpty(localExeSha) && localExeSha.Length >= 8
                ? $", SHA: {localExeSha[..8]}..."
                : "";
            string toolCommitInfo = !string.IsNullOrEmpty(localCommitSha) && localCommitSha.Length >= 7
                ? $", Rev: {localCommitSha[..7]}"
                : "";

            string path = _settingsService.Current.GamePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string toolInfo = HasAppUpdate
                    ? $"\n\n★ Có bản cập nhật Tool mới: v{AvailableAppUpdate?.Version}"
                    : $"\n\nTool DBAM đang ở phiên bản mới nhất (v{AppVersion}{toolShaInfo}{toolCommitInfo}).";
                _dialogService.ShowInfo("Kiểm tra phiên bản & SHA", $"Trạng thái Game Client: {AssemblyStatus}\n{UpdateStatusMessage}{toolInfo}");
            }
            else
            {
                if (!HasAppUpdate)
                {
                    _dialogService.ShowInfo("Kiểm tra phiên bản & SHA", $"Tool DBAM đang ở phiên bản mới nhất (v{AppVersion}{toolShaInfo}{toolCommitInfo}).\n(Chưa chọn file game .exe)");
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Không thể kiểm tra cập nhật: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task ApplyAppUpdateAsync()
    {
        if (AvailableAppUpdate == null || IsUpdatingApp) return;

        IsUpdatingApp = true;
        UpdateProgress = 0;
        AppUpdateStatusText = "Đang kết nối máy chủ cập nhật...";

        try
        {
            var progress = new Progress<double>(p =>
            {
                UpdateProgress = p;
                AppUpdateStatusText = $"Đang tải bản cập nhật: {p:F1}%";
            });

            await _updaterService.ApplyUpdateAsync(AvailableAppUpdate, progress);
        }
        catch (Exception ex)
        {
            IsUpdatingApp = false;
            AppUpdateStatusText = string.Empty;
            _dialogService.ShowError("Lỗi cập nhật", $"Không thể hoàn tất cập nhật: {ex.Message}");
        }
    }

    [RelayCommand]
    private void OpenGameFolder()
    {
        string path = _settingsService.Current.GamePath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                string dir = Path.GetDirectoryName(path)!;
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError("Lỗi mở thư mục", ex.Message);
            }
        }
        else
        {
            _dialogService.ShowWarning("Thông báo", "Chưa cài đặt đường dẫn game hợp lệ.");
        }
    }
}
