using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
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
    /// Lấy mã Git commit SHA của phiên bản hiện tại.
    /// </summary>
    public string GetCurrentCommitSha()
    {
        return GetCurrentCommitShaInternal();
    }

    /// <summary>
    /// Lấy mã SHA256 của file thực thi hiện tại.
    /// </summary>
    public string GetCurrentExeSha256()
    {
        return GetCurrentExeSha256Internal();
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
    /// Tải file exe mới, xác thực SHA256, hoán đổi file và khởi động lại ứng dụng.
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

    public static string GetCurrentCommitShaStatic() => GetCurrentCommitShaInternal();

    public static string GetCurrentExeSha256Static() => GetCurrentExeSha256Internal();

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

    private static string GetCurrentCommitShaInternal()
    {
        try
        {
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrEmpty(infoVer))
            {
                var parts = infoVer.Split('+');
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    return parts[1].Trim();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppUpdater] Không thể lấy commit SHA hiện tại: {ex.Message}");
        }

        return string.Empty;
    }

    private static string GetCurrentExeSha256Internal()
    {
        string currentExe = Environment.ProcessPath ?? "";
        if (!string.IsNullOrEmpty(currentExe) && File.Exists(currentExe))
        {
            try
            {
                using var stream = new FileStream(currentExe, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var hash = SHA256.HashData(stream);
                return Convert.ToHexString(hash);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppUpdater] Không thể tính SHA256 exe hiện tại: {ex.Message}");
            }
        }
        return string.Empty;
    }

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

        string localCommitSha = GetCurrentCommitShaInternal();
        string localExeSha = GetCurrentExeSha256Internal();

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

        // Trích xuất remote commit SHA nếu có trong target_commitish
        string? remoteCommitSha = null;
        if (json.TryGetProperty("target_commitish", out var commitProp))
        {
            string targetCommit = commitProp.GetString()?.Trim() ?? "";
            if (targetCommit.Length == 40 && Regex.IsMatch(targetCommit, "^[a-fA-F0-9]{40}$"))
            {
                remoteCommitSha = targetCommit;
            }
        }

        string notes = json.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";

        // Thử trích xuất commit SHA từ release notes nếu target_commitish là tên branch
        if (string.IsNullOrEmpty(remoteCommitSha) && !string.IsNullOrWhiteSpace(notes))
        {
            var commitMatch = Regex.Match(notes, @"(?:Commit|Rev)[\s:=]+([a-fA-F0-9]{40})", RegexOptions.IgnoreCase);
            if (commitMatch.Success)
            {
                remoteCommitSha = commitMatch.Groups[1].Value;
            }
        }

        // Thử trích xuất SHA256 từ release notes
        string? remoteSha256 = null;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            var shaMatch = Regex.Match(notes, @"(?:SHA-?256|Hash|Checksum)[\s:=]+([a-fA-F0-9]{64})", RegexOptions.IgnoreCase);
            if (shaMatch.Success)
            {
                remoteSha256 = shaMatch.Groups[1].Value.ToUpperInvariant();
            }
        }

        string? downloadUrl = null;
        long? sizeBytes = null;
        string? assetName = null;
        string? shaAssetUrl = null;

        if (json.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
        {
            var assetList = assetsProp.EnumerateArray().ToList();

            // 1. Tìm asset chính chứa từ khóa chỉ định (vd: "GUI" hoặc "CLI") và có đuôi .exe
            foreach (var asset in assetList)
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
                foreach (var asset in assetList)
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

            // 3. Tìm companion checksum/sha256 asset nếu chưa có SHA256 từ notes
            if (!string.IsNullOrEmpty(assetName))
            {
                foreach (var asset in assetList)
                {
                    string name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Equals($"{assetName}.sha256", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals($"{Path.GetFileNameWithoutExtension(assetName)}.sha256", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("sha256.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        shaAssetUrl = asset.GetProperty("browser_download_url").GetString();
                        break;
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(downloadUrl))
        {
            return null;
        }

        // Tải SHA256 từ companion asset nếu có
        if (string.IsNullOrEmpty(remoteSha256) && !string.IsNullOrEmpty(shaAssetUrl))
        {
            try
            {
                string shaText = await Http.GetStringAsync(shaAssetUrl, ct);
                var m = Regex.Match(shaText, @"[a-fA-F0-9]{64}");
                if (m.Success)
                {
                    remoteSha256 = m.Value.ToUpperInvariant();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppUpdater] Không thể tải file SHA256 phụ trợ: {ex.Message}");
            }
        }

        // Kiểm tra phiên bản và mã SHA
        bool hasNewerVersion = false;
        bool isSameVersion = false;

        if (Version.TryParse(verStr, out var newVersion))
        {
            if (currentVersion == null || newVersion > currentVersion)
            {
                hasNewerVersion = true;
            }
            else if (newVersion == currentVersion)
            {
                isSameVersion = true;
            }
        }

        bool isShaMismatch = false;
        if (isSameVersion)
        {
            // Kiểm tra Git commit SHA nếu có
            if (!string.IsNullOrEmpty(remoteCommitSha) && !string.IsNullOrEmpty(localCommitSha))
            {
                if (!string.Equals(remoteCommitSha, localCommitSha, StringComparison.OrdinalIgnoreCase))
                {
                    isShaMismatch = true;
                }
            }
            // Hoặc kiểm tra file SHA256 nếu có
            else if (!string.IsNullOrEmpty(remoteSha256) && !string.IsNullOrEmpty(localExeSha))
            {
                if (!string.Equals(remoteSha256, localExeSha, StringComparison.OrdinalIgnoreCase))
                {
                    isShaMismatch = true;
                }
            }
        }

        // Nếu không có phiên bản mới hơn và mã SHA cũng trùng khớp -> Đang ở bản mới nhất
        if (!hasNewerVersion && !isShaMismatch)
        {
            return null;
        }

        bool isShaUpdateOnly = !hasNewerVersion && isShaMismatch;
        return new AppUpdateInfo(tag, notes, downloadUrl, sizeBytes, assetName, remoteSha256, remoteCommitSha, isShaUpdateOnly);
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

            // Xác thực mã SHA256 của file tải về nếu có thông tin SHA từ release
            if (!string.IsNullOrEmpty(update.RemoteSha256))
            {
                string downloadedSha256;
                using (var stream = new FileStream(tempExe, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    var hash = await SHA256.HashDataAsync(stream, ct);
                    downloadedSha256 = Convert.ToHexString(hash);
                }

                if (!string.Equals(downloadedSha256, update.RemoteSha256, StringComparison.OrdinalIgnoreCase))
                {
                    if (File.Exists(tempExe))
                    {
                        try { File.Delete(tempExe); } catch { }
                    }

                    throw new InvalidOperationException(
                        $"Xác thực tính toàn vẹn (SHA256) thất bại!\n" +
                        $"Mã SHA256 tải về: {downloadedSha256}\n" +
                        $"Mã SHA256 phát hành: {update.RemoteSha256}\n" +
                        $"Cập nhật đã bị hủy bỏ để đảm bảo an toàn.");
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
