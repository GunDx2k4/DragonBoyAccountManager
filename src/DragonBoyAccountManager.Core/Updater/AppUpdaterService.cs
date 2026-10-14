using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DragonBoyAccountManager.Core.Updater;

public sealed class AppUpdaterService : IAppUpdaterService
{
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "DragonBoyAccountManager-Updater" } }
    };

    /// <summary>
    /// Dọn dẹp file .old từ lần cập nhật trước (nếu có).
    /// </summary>
    public void CleanUpOld()
    {
        CleanUpOldInternal();
    }

    /// <summary>
    /// Kiểm tra phiên bản mới từ GitHub Release. Trả về AppUpdateInfo nếu có bản mới, ngược lại null.
    /// </summary>
    public Task<AppUpdateInfo?> CheckForUpdatesAsync(
        string repo = "GunDx2k4/DragonBoyAccountManager",
        Version? currentVersion = null,
        string? assetKeyword = "GUI",
        CancellationToken ct = default)
    {
        return CheckAsyncInternal(repo, currentVersion, assetKeyword, ct);
    }

    /// <summary>
    /// Tải file exe mới, hoán đổi file và khởi động lại ứng dụng.
    /// </summary>
    public Task ApplyUpdateAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        return ApplyAsyncInternal(update, progress, ct);
    }

    #region Static Helper Methods

    public static void CleanUpOldStatic() => CleanUpOldInternal();

    public static Task<AppUpdateInfo?> CheckAsync(
        string repo = "GunDx2k4/DragonBoyAccountManager",
        Version? currentVersion = null,
        string? assetKeyword = "GUI",
        CancellationToken ct = default)
    {
        return CheckAsyncInternal(repo, currentVersion, assetKeyword, ct);
    }

    public static Task ApplyAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        return ApplyAsyncInternal(update, progress, ct);
    }

    #endregion

    #region Core Logic

    private static void CleanUpOldInternal()
    {
        string currentExe = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(currentExe)) return;

        string oldExe = currentExe + ".old";
        if (File.Exists(oldExe))
        {
            try
            {
                File.Delete(oldExe);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppUpdater] Không thể xóa file cũ {oldExe}: {ex.Message}");
            }
        }
    }

    private static async Task<AppUpdateInfo?> CheckAsyncInternal(
        string repo,
        Version? currentVersion,
        string? assetKeyword,
        CancellationToken ct)
    {
        CleanUpOldInternal();

        currentVersion ??= Assembly.GetEntryAssembly()?.GetName().Version
                        ?? Assembly.GetExecutingAssembly().GetName().Version;

        string url = $"https://api.github.com/repos/{repo}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

        if (!json.TryGetProperty("tag_name", out var tagProp))
        {
            return null;
        }

        string rawTag = tagProp.GetString()?.Trim() ?? "";
        string tag = rawTag.TrimStart('v', 'V');

        // Lấy phần số phiên bản nếu tag có hậu tố (vd: 1.0.0-beta)
        var match = Regex.Match(tag, @"^\d+(\.\d+)+");
        string verStr = match.Success ? match.Value : tag;

        if (!Version.TryParse(verStr, out var newVersion) || (currentVersion != null && newVersion <= currentVersion))
        {
            return null;
        }

        string? downloadUrl = null;
        long? sizeBytes = null;
        string? assetName = null;

        if (json.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
        {
            // 1. Tìm asset chứa từ khóa chỉ định (vd: "GUI" hoặc "CLI") và có đuôi .exe
            foreach (var asset in assetsProp.EnumerateArray())
            {
                string name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    (!string.IsNullOrEmpty(assetKeyword) && name.Contains(assetKeyword, StringComparison.OrdinalIgnoreCase)))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString();
                    if (asset.TryGetProperty("size", out var sizeProp))
                    {
                        sizeBytes = sizeProp.GetInt64();
                    }
                    assetName = name;
                    break;
                }
            }

            // 2. Dự phòng: Lấy bất kỳ asset .exe nào nếu không tìm thấy từ khóa
            if (string.IsNullOrEmpty(downloadUrl))
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        if (asset.TryGetProperty("size", out var sizeProp))
                        {
                            sizeBytes = sizeProp.GetInt64();
                        }
                        assetName = name;
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            return null;
        }

        string notes = json.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
        return new AppUpdateInfo(tag, notes, downloadUrl, sizeBytes, assetName);
    }

    private static async Task ApplyAsyncInternal(
        AppUpdateInfo update,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        string currentExe = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
        {
            throw new InvalidOperationException("Không thể xác định file thực thi hiện tại của ứng dụng.");
        }

        string oldExe = currentExe + ".old";
        string tempExe = Path.Combine(Path.GetTempPath(), $"dbam_update_{Guid.NewGuid():N}.exe");

        try
        {
            using var response = await Http.GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? update.SizeBytes ?? -1;
            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using (var fileStream = new FileStream(tempExe, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    totalRead += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        progress.Report((double)totalRead / totalBytes * 100.0);
                    }
                }
            }

            // Hoán đổi file: current -> old, temp -> current
            if (File.Exists(oldExe))
            {
                try { File.Delete(oldExe); } catch { }
            }

            File.Move(currentExe, oldExe);
            File.Move(tempExe, currentExe);

            // Khởi động lại ứng dụng mới
            Process.Start(new ProcessStartInfo(currentExe)
            {
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(currentExe)
            });

            Environment.Exit(0);
        }
        catch
        {
            if (File.Exists(tempExe))
            {
                try { File.Delete(tempExe); } catch { }
            }
            throw;
        }
    }

    #endregion
}
