namespace DragonBoyAccountManager.GUI.Services.Updater;

public interface IAppUpdaterService
{
    string CurrentVersion => AppUpdaterService.CurrentVersionString;
    AppUpdateInfo? LatestUpdate { get; }
    event Action<AppUpdateInfo?>? UpdateDetected;

    void CleanUpOld();

    Task<AppUpdateInfo?> CheckForUpdatesAsync(
        string repo = "GunDx2k4/DragonBoyAccountManager",
        Version? currentVersion = null,
        string? assetKeyword = null,
        CancellationToken ct = default);

    Task ApplyUpdateAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default);

    Task<string?> GetLatestReleaseNotesAsync(
        string repo = "GunDx2k4/DragonBoyAccountManager",
        CancellationToken ct = default);

    string GetCurrentCommitSha();

    string GetCurrentExeSha256();
}
