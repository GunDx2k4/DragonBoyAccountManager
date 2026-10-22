using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Sessions.Validation;
using DragonBoyAccountManager.GUI.Domain.Messaging;
using DragonBoyAccountManager.GUI.Domain.Updates;
using DragonBoyAccountManager.GUI.Services.Dialogs;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Settings;
using DragonBoyAccountManager.GUI.Services.Updater;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Guide;

public sealed partial class GuideViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IGameEnvironmentService _envService;
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

    [ObservableProperty]
    private bool _isAssemblyMissing = true;

    public bool CanApplyUpdate => HasAppUpdate && !IsUpdatingApp;

    public GuideViewModel(
        ISettingsService settingsService,
        IDialogService dialogService,
        IGameEnvironmentService envService,
        IAppUpdaterService updaterService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _envService = envService;
        _updaterService = updaterService;
        _appVersion = _updaterService.CurrentVersion;

        _releaseNotes = LoadReleaseNotes();
        SyncWithEnvironment();
        _envService.EnvironmentChanged += SyncWithEnvironment;
        _ = CheckAppUpdateOnStartupAsync();
    }

    public string LoadReleaseNotes()
    {
        try
        {
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

    private async Task<bool> PromptAndApplyUpdateAsync(AppUpdateInfo update, bool navigateToGuideOnConfirm)
    {
        AvailableAppUpdate = update;
        HasAppUpdate = true;
        WeakReferenceMessenger.Default.Send(
            new StatusMessageChangedMessage($"[CẬP NHẬT] Đã có bản mới v{update.Version}! Vào tab Hướng dẫn để xem chi tiết."));

        string size = update.SizeBytes.HasValue ? $" ({update.SizeBytes.Value / 1024.0 / 1024.0:F1} MB)" : "";
        string notes = !string.IsNullOrWhiteSpace(update.ReleaseNotes) ? $"\n\nNội dung cập nhật:\n{update.ReleaseNotes}" : "";

        string title = update.IsShaUpdateOnly ? "CẬP NHẬT TOOL DBAM (BẢN VÁ)" : "ĐÃ CÓ PHIÊN BẢN MỚI!";
        string desc = update.IsShaUpdateOnly
            ? $"Phát hiện bản vá mới cho phiên bản: v{update.Version}{size}\nPhiên bản hiện tại: v{AppVersion}{notes}\n\nBạn có muốn cập nhật ngay bây giờ không?"
            : $"Phát hiện phiên bản mới: v{update.Version}{size}\nPhiên bản hiện tại: v{AppVersion}{notes}\n\nBạn có muốn cập nhật ngay bây giờ không?";

        bool confirm = _dialogService.ShowConfirmation(title, desc, yesText: "Cập nhật ngay", noText: "Để sau");
        if (confirm)
        {
            if (navigateToGuideOnConfirm)
            {
                WeakReferenceMessenger.Default.Send(new RequestNavigateToTabMessage(AppTab.Guide));
            }
            await ApplyAppUpdateAsync();
            return true;
        }

        return false;
    }

    public async Task CheckAppUpdateOnStartupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Delay(1200, cancellationToken);

            var currentVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            var update = await _updaterService.CheckForUpdatesAsync("GunDx2k4/DragonBoyAccountManager", currentVer);
            if (update != null)
            {
                await PromptAndApplyUpdateAsync(update, navigateToGuideOnConfirm: true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuideViewModel] Lỗi kiểm tra cập nhật khi khởi động: {ex.Message}");
        }
    }

    public Task LoadSha256ChecksumsAsync() => _envService.LoadChecksumsAndVerifyAsync();

    private void SyncWithEnvironment()
    {
        Status = _envService.Status;
        AssemblyStatus = _envService.AssemblyStatus;
        AssemblyStatusTooltip = _envService.AssemblyStatusTooltip;
        AssemblyPath = _envService.AssemblyPath;
        GameVersion = _envService.GameVersion;
        IsAssemblyMissing = _envService.IsAssemblyMissing;

        var metadata = _envService.CurrentMetadata;
        if (metadata != null)
        {
            AssemblyHash = metadata.ActualSha256 ?? string.Empty;

            switch (metadata.Status)
            {
                case ClientStatus.OfficialVersionMatch:
                    UpdateStatusMessage = $"Phiên bản game hiện tại hoàn toàn khớp với bản gốc chính thức v{metadata.DetectedVersion}. Hệ thống hoạt động tốt.";
                    break;
                case ClientStatus.OutdatedVersion:
                    string oldVerState = metadata.IsShaMatch ? "Bản gốc cũ" : "Bản mod cũ";
                    UpdateStatusMessage = $"Phiên bản game v{metadata.DetectedVersion} ({oldVerState}) đã cũ hơn phiên bản mới nhất v{metadata.TargetVersion}. Vui lòng tải bản mới tại ngocrongonline.com.";
                    break;
                case ClientStatus.ModifiedOrMod:
                    UpdateStatusMessage = metadata.IsCorrectVersion
                        ? $"Đúng phiên bản v{metadata.DetectedVersion} nhưng không khớp mã băm gốc SHA-256. Phát hiện file game đã qua can thiệp (Bản Mod)!"
                        : "Phát hiện file game không hợp lệ (Bản Mod) và không đúng phiên bản.";
                    break;
                case ClientStatus.GameNotFound:
                    UpdateStatusMessage = "Chưa thiết lập đường dẫn game. Vui lòng bấm \"Chọn file game (.exe)\" để bắt đầu.";
                    break;
                case ClientStatus.AssemblyNotFound:
                    UpdateStatusMessage = "Đã nhận file .exe nhưng không tìm thấy Assembly-CSharp.dll trong Data\\Managed.";
                    break;
                case ClientStatus.Error:
                    UpdateStatusMessage = $"Lỗi: {metadata.ErrorMessage}";
                    break;
            }
        }
    }

    public void CheckAssemblyStatus()
    {
        _envService.CheckStatus();
        SyncWithEnvironment();
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        IsCheckingUpdate = true;
        UpdateStatusMessage = "Đang kiểm tra phiên bản...";
        try
        {
            await Task.Delay(200, cancellationToken);
            await LoadSha256ChecksumsAsync();

            AppUpdateInfo? update = null;
            try
            {
                var currentVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                update = await _updaterService.CheckForUpdatesAsync("GunDx2k4/DragonBoyAccountManager", currentVer);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GuideViewModel] Lỗi kiểm tra cập nhật DBAM: {ex.Message}");
            }

            if (update != null)
            {
                if (await PromptAndApplyUpdateAsync(update, navigateToGuideOnConfirm: false))
                {
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

            string path = _envService.GamePath;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string toolInfo = HasAppUpdate
                    ? $"\n\n[MỚI] Có bản cập nhật Tool mới: v{AvailableAppUpdate?.Version}"
                    : $"\n\nTool DBAM đang ở phiên bản mới nhất (v{AppVersion}).";
                _dialogService.ShowInfo("Kiểm tra phiên bản", $"Trạng thái Game Client: {AssemblyStatus}\n{UpdateStatusMessage}{toolInfo}");
            }
            else if (!HasAppUpdate)
            {
                _dialogService.ShowInfo("Kiểm tra phiên bản", $"Tool DBAM đang ở phiên bản mới nhất (v{AppVersion}).\n(Chưa chọn file game .exe)");
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
    private void BrowseGamePath() => _envService.BrowseAndSelectGamePath(_dialogService);

    [RelayCommand]
    private void OpenGameFolder()
    {
        string path = _envService.GamePath;
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
