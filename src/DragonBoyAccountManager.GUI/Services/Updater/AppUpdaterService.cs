using System.Net.Http;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DragonBoyAccountManager.GUI.Services.Updater;

public sealed class AppUpdaterService : IAppUpdaterService
{
    public static string CurrentVersionString { get; } = ResolveCurrentVersionString();

    public string CurrentVersion => CurrentVersionString;
    public AppUpdateInfo? LatestUpdate { get; private set; }
    public event Action<AppUpdateInfo?>? UpdateDetected;
    private static readonly HttpClient Http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "DragonBoyAccountManager-Updater" } }
    };

    private static string ResolveCurrentVersionString()
    {
        var version = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).GetName().Version;
        return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
    }

    public void CleanUpOld()
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

        string dir = Path.GetDirectoryName(currentExe) ?? "";
        string fileName = Path.GetFileName(currentExe);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, $"{fileName}.old*"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }
    }

    public string GetCurrentCommitSha()
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

    public string GetCurrentExeSha256()
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

    private static Version NormalizeVersion(Version v)
    {
        return new Version(
            Math.Max(0, v.Major),
            Math.Max(0, v.Minor),
            Math.Max(0, v.Build),
            Math.Max(0, v.Revision));
    }

    private static string ConvertHtmlToMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        string text = html;
        text = Regex.Replace(text, @"<\s*h1[^>]*>([\s\S]*?)<\s*/\s*h1\s*>", "# $1\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*h2[^>]*>([\s\S]*?)<\s*/\s*h2\s*>", "## $1\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*h3[^>]*>([\s\S]*?)<\s*/\s*h3\s*>", "### $1\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*h4[^>]*>([\s\S]*?)<\s*/\s*h4\s*>", "#### $1\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*(?:strong|b)[^>]*>([\s\S]*?)<\s*/\s*(?:strong|b)\s*>", "**$1**", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*(?:em|i)[^>]*>([\s\S]*?)<\s*/\s*(?:em|i)\s*>", "*$1*", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*(?:code|tt)[^>]*>([\s\S]*?)<\s*/\s*(?:code|tt)\s*>", "`$1`", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*li[^>]*>([\s\S]*?)<\s*/\s*li\s*>", "- $1\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*(?:br|hr)\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*p[^>]*>([\s\S]*?)<\s*/\s*p\s*>", "$1\n\n", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<\s*a\s+[^>]*href=[""']([^""']*)[""'][^>]*>([\s\S]*?)<\s*/\s*a\s*>", "[$2]($1)", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"<[^>]+>", string.Empty, RegexOptions.IgnoreCase);
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\r\n|\r", "\n");
        text = Regex.Replace(text, @"\n{3,}", "\n\n");
        return text.Trim();
    }

    private async Task<string> FetchReleaseNotesMarkdownAsync(string repo, string tag, string rawHtmlContent, CancellationToken ct)
    {
        try
        {
            string rawUrl = $"https://raw.githubusercontent.com/{repo}/{tag}/RELEASE_NOTES.md";
            using var rawReq = new HttpRequestMessage(HttpMethod.Get, rawUrl);
            using var rawResp = await Http.SendAsync(rawReq, ct);
            if (rawResp.IsSuccessStatusCode)
            {
                string rawText = await rawResp.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    return rawText.Trim();
                }
            }
        }
        catch { }

        try
        {
            string rawUrl = $"https://raw.githubusercontent.com/{repo}/main/RELEASE_NOTES.md";
            using var rawReq = new HttpRequestMessage(HttpMethod.Get, rawUrl);
            using var rawResp = await Http.SendAsync(rawReq, ct);
            if (rawResp.IsSuccessStatusCode)
            {
                string rawText = await rawResp.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    return rawText.Trim();
                }
            }
        }
        catch { }

        return ConvertHtmlToMarkdown(rawHtmlContent);
    }

    public async Task<AppUpdateInfo?> CheckForUpdatesAsync(
        string repo = "GunDx2k4/DragonBoyAccountManager",
        Version? currentVersion = null,
        string? assetKeyword = null,
        CancellationToken ct = default)
    {
        CleanUpOld();

        currentVersion ??= Assembly.GetEntryAssembly()?.GetName().Version
                        ?? Assembly.GetExecutingAssembly().GetName().Version
                        ?? new Version(1, 0, 0, 0);

        string localCommitSha = GetCurrentCommitSha();
        string localExeSha = GetCurrentExeSha256();

        try
        {
            string url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await Http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                return await CheckForUpdatesViaWebFallbackAsync(repo, currentVersion, assetKeyword, localCommitSha, localExeSha, ct);
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);

            if (!json.TryGetProperty("tag_name", out var tagProp))
            {
                return await CheckForUpdatesViaWebFallbackAsync(repo, currentVersion, assetKeyword, localCommitSha, localExeSha, ct);
            }

            string rawTag = tagProp.GetString()?.Trim() ?? "";
            string tag = rawTag.TrimStart('v', 'V');

            var match = Regex.Match(tag, @"^\d+(\.\d+)+");
            string verStr = match.Success ? match.Value : tag;

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

            if (string.IsNullOrEmpty(remoteCommitSha) && !string.IsNullOrWhiteSpace(notes))
            {
                var commitMatch = Regex.Match(notes, @"(?:Commit|Rev)[\s:=]+([a-fA-F0-9]{40})", RegexOptions.IgnoreCase);
                if (commitMatch.Success)
                {
                    remoteCommitSha = commitMatch.Groups[1].Value;
                }
            }

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
                return await CheckForUpdatesViaWebFallbackAsync(repo, currentVersion, assetKeyword, localCommitSha, localExeSha, ct);
            }

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

            bool hasNewerVersion = false;
            bool isSameVersion = false;

            if (Version.TryParse(verStr, out var newVersion))
            {
                var normNew = NormalizeVersion(newVersion);
                var normCur = NormalizeVersion(currentVersion);

                if (normNew > normCur)
                {
                    hasNewerVersion = true;
                }
                else if (normNew == normCur)
                {
                    isSameVersion = true;
                }
            }

            bool isShaMismatch = false;
            if (isSameVersion)
            {
                if (!string.IsNullOrEmpty(remoteCommitSha) && !string.IsNullOrEmpty(localCommitSha))
                {
                    if (!string.Equals(remoteCommitSha, localCommitSha, StringComparison.OrdinalIgnoreCase))
                    {
                        isShaMismatch = true;
                    }
                }
                else if (!string.IsNullOrEmpty(remoteSha256) && !string.IsNullOrEmpty(localExeSha))
                {
                    if (!string.Equals(remoteSha256, localExeSha, StringComparison.OrdinalIgnoreCase))
                    {
                        isShaMismatch = true;
                    }
                }
            }

            if (!hasNewerVersion && !isShaMismatch)
            {
                return null;
            }

            bool isShaUpdateOnly = !hasNewerVersion && isShaMismatch;
            var updateInfo = new AppUpdateInfo(tag, notes, downloadUrl, sizeBytes, assetName, remoteSha256, remoteCommitSha, isShaUpdateOnly);
            LatestUpdate = updateInfo;
            UpdateDetected?.Invoke(updateInfo);
            return updateInfo;
        }
        catch
        {
            return await CheckForUpdatesViaWebFallbackAsync(repo, currentVersion, assetKeyword, localCommitSha, localExeSha, ct);
        }
    }

    private async Task<AppUpdateInfo?> CheckForUpdatesViaWebFallbackAsync(
        string repo,
        Version currentVersion,
        string? assetKeyword,
        string localCommitSha,
        string localExeSha,
        CancellationToken ct)
    {
        try
        {
            string atomUrl = $"https://github.com/{repo}/releases.atom";
            using var atomReq = new HttpRequestMessage(HttpMethod.Get, atomUrl);
            using var atomResp = await Http.SendAsync(atomReq, ct);
            if (!atomResp.IsSuccessStatusCode)
            {
                return null;
            }

            string atomContent = await atomResp.Content.ReadAsStringAsync(ct);
            var entryMatch = Regex.Match(atomContent, @"<entry>([\s\S]*?)</entry>", RegexOptions.IgnoreCase);
            if (!entryMatch.Success)
            {
                return null;
            }

            string entryXml = entryMatch.Groups[1].Value;

            var tagMatch = Regex.Match(entryXml, @"releases/tag/([^""\s<]+)", RegexOptions.IgnoreCase);
            if (!tagMatch.Success)
            {
                return null;
            }

            string rawTag = tagMatch.Groups[1].Value.Trim();
            string tag = rawTag.TrimStart('v', 'V');

            var verMatch = Regex.Match(tag, @"^\d+(\.\d+)+");
            string verStr = verMatch.Success ? verMatch.Value : tag;

            string notes = "";
            var contentMatch = Regex.Match(entryXml, @"<content[^>]*>([\s\S]*?)</content>", RegexOptions.IgnoreCase);
            if (contentMatch.Success)
            {
                notes = await FetchReleaseNotesMarkdownAsync(repo, rawTag, contentMatch.Groups[1].Value, ct);
            }

            string? remoteCommitSha = null;
            var commitMatch = Regex.Match(notes, @"(?:Commit|Rev)[\s:=]+([a-fA-F0-9]{40})", RegexOptions.IgnoreCase);
            if (commitMatch.Success)
            {
                remoteCommitSha = commitMatch.Groups[1].Value;
            }

            string? remoteSha256 = null;
            var shaMatch = Regex.Match(notes, @"(?:SHA-?256|Hash|Checksum)[\s:=]+([a-fA-F0-9]{64})", RegexOptions.IgnoreCase);
            if (shaMatch.Success)
            {
                remoteSha256 = shaMatch.Groups[1].Value.ToUpperInvariant();
            }

            string assetsUrl = $"https://github.com/{repo}/releases/expanded_assets/{rawTag}";
            using var assetsReq = new HttpRequestMessage(HttpMethod.Get, assetsUrl);
            using var assetsResp = await Http.SendAsync(assetsReq, ct);

            string? downloadUrl = null;
            string? assetName = null;
            string? shaAssetUrl = null;

            if (assetsResp.IsSuccessStatusCode)
            {
                string assetsHtml = await assetsResp.Content.ReadAsStringAsync(ct);
                var hrefMatches = Regex.Matches(assetsHtml, @"href=""(/[^""]+/releases/download/[^""]+)""", RegexOptions.IgnoreCase);

                foreach (Match m in hrefMatches)
                {
                    string href = m.Groups[1].Value;
                    string name = Path.GetFileName(href);

                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                        (!string.IsNullOrEmpty(assetKeyword) && name.Contains(assetKeyword, StringComparison.OrdinalIgnoreCase)))
                    {
                        downloadUrl = "https://github.com" + href;
                        assetName = name;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    foreach (Match m in hrefMatches)
                    {
                        string href = m.Groups[1].Value;
                        string name = Path.GetFileName(href);
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = "https://github.com" + href;
                            assetName = name;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(assetName))
                {
                    foreach (Match m in hrefMatches)
                    {
                        string href = m.Groups[1].Value;
                        string name = Path.GetFileName(href);
                        if (name.Equals($"{assetName}.sha256", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals($"{Path.GetFileNameWithoutExtension(assetName)}.sha256", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("checksums.txt", StringComparison.OrdinalIgnoreCase) ||
                            name.Equals("sha256.txt", StringComparison.OrdinalIgnoreCase))
                        {
                            shaAssetUrl = "https://github.com" + href;
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                string defaultAssetName = !string.IsNullOrEmpty(assetKeyword) ? $"DragonBoyAccountManager-{assetKeyword}.exe" : "DragonBoyAccountManager.exe";
                downloadUrl = $"https://github.com/{repo}/releases/download/{rawTag}/{defaultAssetName}";
                assetName = defaultAssetName;
                shaAssetUrl = $"https://github.com/{repo}/releases/download/{rawTag}/{defaultAssetName}.sha256";
            }

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
                catch { }
            }

            bool hasNewerVersion = false;
            bool isSameVersion = false;

            if (Version.TryParse(verStr, out var newVer))
            {
                var normNew = NormalizeVersion(newVer);
                var normCur = NormalizeVersion(currentVersion);

                if (normNew > normCur)
                {
                    hasNewerVersion = true;
                }
                else if (normNew == normCur)
                {
                    isSameVersion = true;
                }
            }

            bool isShaMismatch = false;
            if (isSameVersion)
            {
                if (!string.IsNullOrEmpty(remoteCommitSha) && !string.IsNullOrEmpty(localCommitSha))
                {
                    if (!string.Equals(remoteCommitSha, localCommitSha, StringComparison.OrdinalIgnoreCase))
                    {
                        isShaMismatch = true;
                    }
                }
                else if (!string.IsNullOrEmpty(remoteSha256) && !string.IsNullOrEmpty(localExeSha))
                {
                    if (!string.Equals(remoteSha256, localExeSha, StringComparison.OrdinalIgnoreCase))
                    {
                        isShaMismatch = true;
                    }
                }
            }

            if (!hasNewerVersion && !isShaMismatch)
            {
                return null;
            }

            bool isShaUpdateOnly = !hasNewerVersion && isShaMismatch;
            return new AppUpdateInfo(tag, notes, downloadUrl, null, assetName, remoteSha256, remoteCommitSha, isShaUpdateOnly);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppUpdater] Web fallback lỗi: {ex.Message}");
            return null;
        }
    }

    public async Task ApplyUpdateAsync(
        AppUpdateInfo update,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        string currentExe = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(currentExe) || !File.Exists(currentExe))
        {
            throw new InvalidOperationException("Không thể xác định file thực thi hiện tại của ứng dụng.");
        }

        string exeName = Path.GetFileNameWithoutExtension(currentExe);
        if (exeName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ||
            exeName.Equals("devenv", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Không thể tự động cập nhật khi đang chạy trong môi trường phát triển (dotnet.exe). Vui lòng chạy trực tiếp tệp DragonBoyAccountManager.exe đã xuất bản.");
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
                        "Cập nhật đã bị hủy bỏ để đảm bảo an toàn.");
                }
            }

            if (File.Exists(oldExe))
            {
                try { File.Delete(oldExe); } catch { }
            }

            if (File.Exists(oldExe))
            {
                oldExe = $"{currentExe}.old_{Guid.NewGuid():N}";
            }

            File.Move(currentExe, oldExe);
            File.Move(tempExe, currentExe);

            Process.Start(new ProcessStartInfo(currentExe)
            {
                UseShellExecute = true,
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

    public async Task<string?> GetLatestReleaseNotesAsync(
        string repo = "GunDx2k4/DragonBoyAccountManager",
        CancellationToken ct = default)
    {
        try
        {
            string url = $"https://api.github.com/repos/{repo}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await Http.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
                if (json.TryGetProperty("body", out var bodyProp))
                {
                    string? body = bodyProp.GetString();
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        return body;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppUpdater] Không thể lấy release notes từ API: {ex.Message}");
        }

        try
        {
            string rawUrl = $"https://raw.githubusercontent.com/{repo}/main/RELEASE_NOTES.md";
            using var rawReq = new HttpRequestMessage(HttpMethod.Get, rawUrl);
            using var rawResp = await Http.SendAsync(rawReq, ct);
            if (rawResp.IsSuccessStatusCode)
            {
                string rawText = await rawResp.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    return rawText.Trim();
                }
            }
        }
        catch { }

        try
        {
            string atomUrl = $"https://github.com/{repo}/releases.atom";
            using var atomReq = new HttpRequestMessage(HttpMethod.Get, atomUrl);
            using var atomResp = await Http.SendAsync(atomReq, ct);
            if (atomResp.IsSuccessStatusCode)
            {
                string atomContent = await atomResp.Content.ReadAsStringAsync(ct);
                var contentMatch = Regex.Match(atomContent, @"<content[^>]*>([\s\S]*?)</content>", RegexOptions.IgnoreCase);
                if (contentMatch.Success)
                {
                    var tagMatch = Regex.Match(atomContent, @"releases/tag/([^""\s<]+)", RegexOptions.IgnoreCase);
                    string rawTag = tagMatch.Success ? tagMatch.Groups[1].Value.Trim() : "main";
                    return await FetchReleaseNotesMarkdownAsync(repo, rawTag, contentMatch.Groups[1].Value, ct);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppUpdater] Không thể lấy release notes từ Web fallback: {ex.Message}");
        }

        return null;
    }
}
