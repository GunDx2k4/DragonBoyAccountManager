namespace DragonBoyAccountManager.Core.Updater;

public interface IAppUpdaterService
{
    void CleanUpOld();

    Task<AppUpdateInfo?> CheckForUpdatesAsync(
        string repo = "GunDx2k4/DragonBoyAccountManager",
        Version? currentVersion = null,
        string? assetKeyword = "GUI",
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
