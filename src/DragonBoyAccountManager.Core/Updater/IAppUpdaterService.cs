namespace DragonBoyAccountManager.Core.Updater;

public interface IAppUpdaterService
{
    /// <summary>
    /// Dọn dẹp file .old từ lần cập nhật trước (nếu có).
    /// </summary>
    void CleanUpOld();

    /// <summary>
    /// Kiểm tra phiên bản mới từ GitHub Release. Trả về AppUpdateInfo nếu có bản mới, ngược lại null.
    /// </summary>
    Task<AppUpdateInfo?> CheckForUpdatesAsync(
        string repo = "GunDx2k4/DragonBoyAccountManager",
        Version? currentVersion = null,
        string? assetKeyword = "GUI",
        CancellationToken ct = default);

    /// <summary>
    /// Tải file exe mới, hoán đổi file và khởi động lại ứng dụng.
    /// </summary>
    Task ApplyUpdateAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default);
}
