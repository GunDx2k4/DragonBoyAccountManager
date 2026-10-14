namespace DragonBoyAccountManager.Core.Updater;

public record AppUpdateInfo(
    string Version,
    string ReleaseNotes,
    string DownloadUrl,
    long? SizeBytes = null,
    string? AssetName = null
);
