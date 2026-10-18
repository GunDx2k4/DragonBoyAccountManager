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

    [ObservableProperty]
    private string _releaseNotes = string.Empty;

    public bool CanApplyUpdate => HasAppUpdate && !IsUpdatingApp;

    public event Action? OnRequestNavigateToGuide;

    private Dictionary<string, string>? _versionChecksums;

    public FeedbackViewModel Feedback { get; }

    public GuideViewModel(
        ISettingsService settingsService,
        IDialogService dialogService,
        IGameClientValidator clientValidator,
        IAppUpdaterService updaterService,
        FeedbackViewModel feedback)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _clientValidator = clientValidator;
        _updaterService = updaterService;
        Feedback = feedback;

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        _appVersion = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";

        _releaseNotes = LoadReleaseNotes();
        CheckAssemblyStatus();
        _ = LoadSha256ChecksumsAsync();
        _ = CheckAppUpdateOnStartupAsync();
    }

    public string LoadReleaseNotes()
    {
        try
        {
            // 1. Thử đọc từ tệp RELEASE_NOTES.md ở thư mục chạy hoặc các thư mục cha
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths =
            [
                Path.Combine(baseDir, "RELEASE_NOTES.md"),
                Path.Combine(baseDir, "..", "RELEASE_NOTES.md"),
                Path.Combine(baseDir, "..", "..", "RELEASE_NOTES.md"),
                Path.Combine(baseDir, "..", "..", "..", "RELEASE_NOTES.md"),
                Path.Combine(baseDir, "..", "..", "..", "..", "RELEASE_NOTES.md")
            ];

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
            }

            // 2. Thử đọc từ EmbeddedResource trong assembly
            var asm = typeof(GuideViewModel).Assembly;
            var resourceName = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("RELEASE_NOTES.md", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(resourceName))
            {
                using var stream = asm.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuideViewModel] Lỗi nạp nhật ký phiên bản: {ex.Message}");
        }

        return "Hiện chưa có thông tin nhật ký phiên bản.";
    }

    public async Task CheckAppUpdateOnStartupAsync()
    {
        try
        {
            // Đợi 1.2s để cửa sổ chính MainWindow hiển thị đầy đủ và ổn định
            await Task.Delay(1200);

            var update = await _updaterService.CheckForUpdatesAsync("GunDx2k4/DragonBoyAccountManager", null, "GUI");
            if (update != null)
            {
                AvailableAppUpdate = update;
                HasAppUpdate = true;

                string size = update.SizeBytes.HasValue ? $" ({update.SizeBytes.Value / 1024.0 / 1024.0:F1} MB)" : "";
                string notes = !string.IsNullOrWhiteSpace(update.ReleaseNotes) ? $"\n\nNội dung cập nhật:\n{update.ReleaseNotes}" : "";

                string title = update.IsShaUpdateOnly ? "CẬP NHẬT TOOL DBAM (BẢN VÁ)" : "ĐÃ CÓ PHIÊN BẢN MỚI!";
                string desc = update.IsShaUpdateOnly
                    ? $"Phát hiện bản vá mới cho phiên bản: v{update.Version}{size}\nPhiên bản hiện tại: v{AppVersion}{notes}\n\nBạn có muốn cập nhật ngay bây giờ không?"
                    : $"Phát hiện phiên bản mới: v{update.Version}{size}\nPhiên bản hiện tại: v{AppVersion}{notes}\n\nBạn có muốn cập nhật ngay bây giờ không?";

                bool confirm = _dialogService.ShowConfirmation(title, desc, yesText: "Cập nhật ngay", noText: "Để sau");
                if (confirm)
                {
                    OnRequestNavigateToGuide?.Invoke();
                    await ApplyAppUpdateAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuideViewModel] Lỗi kiểm tra cập nhật khi khởi động: {ex.Message}");
        }
    }

    public async Task CheckAppUpdateSilentlyAsync()
    {
        await CheckAppUpdateOnStartupAsync();
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
                AssemblyStatus = $"v{metadata.DetectedVersion} (Khớp bản gốc)";
                UpdateStatusMessage = $"Khớp hoàn toàn với phiên bản gốc chính thức v{metadata.DetectedVersion}.";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} bytes\n" +
                                         $"Phiên bản: v{metadata.DetectedVersion} (Khớp bản gốc chính thức)";
                break;

            case ClientStatus.OutdatedVersion:
                AssemblyStatus = $"Cần cập nhật v{metadata.TargetVersion}";
                string oldVerState = metadata.IsShaMatch ? "Bản gốc cũ" : "Bản mod cũ";
                UpdateStatusMessage = $"Game đang ở phiên bản cũ v{metadata.DetectedVersion} ({oldVerState}), bản mới nhất là v{metadata.TargetVersion}.";
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} bytes\n" +
                                         $"Phiên bản tệp: v{metadata.DetectedVersion}\n" +
                                         $"Phiên bản mới nhất: v{metadata.TargetVersion}\n" +
                                         $"Trạng thái: Cần cập nhật ({oldVerState})";
                break;

            case ClientStatus.ModifiedOrMod:
                if (metadata.IsCorrectVersion)
                {
                    AssemblyStatus = $"v{metadata.DetectedVersion} (Không khớp - Bản mod)";
                    UpdateStatusMessage = $"Đúng phiên bản v{metadata.DetectedVersion} nhưng không khớp bản gốc (File game đã bị can thiệp hoặc là bản mod).";
                }
                else
                {
                    AssemblyStatus = "Không khớp (Bản mod)";
                    UpdateStatusMessage = "Không khớp phiên bản và không khớp bản gốc (File game đã bị can thiệp hoặc là bản mod).";
                }
                AssemblyStatusTooltip = $"Tệp: {Path.GetFileName(metadata.AssemblyPath)}\n" +
                                         $"Đường dẫn: {metadata.AssemblyPath}\n" +
                                         $"Kích thước: {metadata.FileSizeBytes:N0} bytes\n" +
                                         $"Phiên bản phát hiện: v{metadata.DetectedVersion ?? "Không xác định"}\n" +
                                         $"Phiên bản mục tiêu: v{metadata.TargetVersion}\n" +
                                         $"Trạng thái: Không khớp bản gốc (Bản mod hoặc đã can thiệp)";
                break;

            case ClientStatus.AssemblyNotFound:
                AssemblyStatus = "Không tìm thấy Assembly";
                UpdateStatusMessage = "Không tìm thấy file Assembly-CSharp.dll trong thư mục dữ liệu Data\\Managed.";
                AssemblyStatusTooltip = $"Đã nhận file game: {Path.GetFileName(gamePath)}\nNhưng không tìm thấy Assembly-CSharp.dll trong Data\\Managed.";
                break;

            case ClientStatus.Error:
                AssemblyStatus = "Lỗi đọc file";
                UpdateStatusMessage = $"Lỗi: {metadata.ErrorMessage}";
                AssemblyStatusTooltip = metadata.ErrorMessage ?? "Lỗi đọc file";
                break;

            case ClientStatus.GameNotFound:
            default:
                AssemblyStatus = "Chưa chọn file game .exe";
                UpdateStatusMessage = "Đường dẫn game bắt buộc phải là file thực thi (.exe).\nVui lòng chọn file game ở thanh đáy cửa sổ.";
                AssemblyStatusTooltip = "Chưa cài đặt đường dẫn game hợp lệ.";
                break;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingUpdate = true;
        UpdateStatusMessage = "Đang kiểm tra phiên bản...";
        try
        {
            await Task.Delay(200);
            await LoadSha256ChecksumsAsync();

            // Kiểm tra cập nhật tool DBAM từ GitHub Releases
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

                string title = update.IsShaUpdateOnly ? "Cập nhật Tool DBAM (Bản vá)" : "Cập nhật Tool DBAM";
                string desc = update.IsShaUpdateOnly
                    ? $"Phát hiện bản vá mới cho phiên bản: v{update.Version}{size}\nPhiên bản hiện tại: v{AppVersion}{notes}\n\nBạn có muốn cập nhật ngay bây giờ không?"
                    : $"Phát hiện phiên bản mới: v{update.Version}{size}\nPhiên bản hiện tại: v{AppVersion}{notes}\n\nBạn có muốn cập nhật ngay bây giờ không?";

                bool confirm = _dialogService.ShowConfirmation(title, desc, yesText: "Cập nhật ngay", noText: "Để sau");

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

                try
                {
                    var latestNotes = await _updaterService.GetLatestReleaseNotesAsync();
                    if (!string.IsNullOrWhiteSpace(latestNotes))
                    {
                        ReleaseNotes = latestNotes;
                    }
                }
                catch { }
            }

            string path = _settingsService.Current.GamePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string toolInfo = HasAppUpdate
                    ? $"\n\n★ Có bản cập nhật Tool mới: v{AvailableAppUpdate?.Version}"
                    : $"\n\nTool DBAM đang ở phiên bản mới nhất (v{AppVersion}).";
                _dialogService.ShowInfo("Kiểm tra phiên bản", $"Trạng thái Game Client: {AssemblyStatus}\n{UpdateStatusMessage}{toolInfo}");
            }
            else
            {
                if (!HasAppUpdate)
                {
                    _dialogService.ShowInfo("Kiểm tra phiên bản", $"Tool DBAM đang ở phiên bản mới nhất (v{AppVersion}).\n(Chưa chọn file game .exe)");
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
