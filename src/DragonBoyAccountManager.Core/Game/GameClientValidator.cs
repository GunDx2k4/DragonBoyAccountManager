using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using DragonBoyAccountManager.Core.Common;

namespace DragonBoyAccountManager.Core.Game;

public class GameClientValidator : IGameClientValidator, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _sha256Url;
    private readonly ConcurrentDictionary<string, string> _versionChecksums = new(StringComparer.OrdinalIgnoreCase);

    public GameClientValidator(HttpClient? httpClient = null, string? sha256Url = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "DragonBoyAccountManager/1.0");
            _ownsHttpClient = true;
        }

        _sha256Url = !string.IsNullOrWhiteSpace(sha256Url) ? sha256Url : Constants.Client.DefaultSha256Url;
        _versionChecksums[Constants.Client.DefaultVersion] = Constants.Client.DefaultSha256;
    }

    public bool CheckIsValidGameExe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        var cleanPath = path.Trim().Trim('"', '\'');
        return File.Exists(cleanPath) && string.Equals(Path.GetExtension(cleanPath), ".exe", StringComparison.OrdinalIgnoreCase);
    }

    public string? ResolveExePath(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath)) return null;
        gamePath = gamePath.Trim().Trim('"', '\'');

        return File.Exists(gamePath) && gamePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? gamePath
            : null;
    }

    public string? ResolveAssemblyPath(string? gamePath) => ResolveGamePaths(gamePath)?.AssemblyPath;

    public ResolvedGamePaths? ResolveGamePaths(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath)) return null;

        gamePath = gamePath.Trim().Trim('"', '\'');
        if (!File.Exists(gamePath)) return null;

        var dir = Path.GetDirectoryName(gamePath);
        if (string.IsNullOrEmpty(dir)) return null;

        var exeName = Path.GetFileNameWithoutExtension(gamePath);

        var defaultData = Path.Combine(dir, $"{exeName}_Data");
        var defaultManaged = Path.Combine(defaultData, Constants.Client.ManagedFolder);
        var defaultAssembly = Path.Combine(defaultManaged, Constants.Client.AssemblyName);
        if (File.Exists(defaultAssembly))
            return new ResolvedGamePaths(defaultData, defaultManaged, defaultAssembly);

        try
        {
            var dataDirs = Directory.GetDirectories(dir, "*_Data");
            foreach (var d in dataDirs)
            {
                var candidateManaged = Path.Combine(d, Constants.Client.ManagedFolder);
                var candidate = Path.Combine(candidateManaged, Constants.Client.AssemblyName);
                if (File.Exists(candidate))
                    return new ResolvedGamePaths(d, candidateManaged, candidate);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }

        var fallbackManaged = Path.Combine(dir, Constants.Client.ManagedFolder);
        var fallbackAssembly = Path.Combine(fallbackManaged, Constants.Client.AssemblyName);
        if (File.Exists(fallbackAssembly))
            return new ResolvedGamePaths(dir, fallbackManaged, fallbackAssembly);

        return null;
    }

    public string ComputeFileSha256(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var bytes = SHA256.HashData(stream);
        return Convert.ToHexString(bytes);
    }

    public async Task<Dictionary<string, string>> GetSha256ChecksumsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _sha256Url);
            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        var ver = prop.Name;
                        string sha = string.Empty;

                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            sha = prop.Value.GetString() ?? string.Empty;
                        }
                        else if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            if (prop.Value.TryGetProperty("sha256", out var sProp) || prop.Value.TryGetProperty("Sha256", out sProp))
                            {
                                sha = sProp.GetString() ?? string.Empty;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(sha))
                        {
                            _versionChecksums[ver] = sha.Trim();
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
        }

        return new Dictionary<string, string>(_versionChecksums, StringComparer.OrdinalIgnoreCase);
    }

    public ClientMetadata InspectClientMetadata(string? gamePath, IReadOnlyDictionary<string, string>? versionChecksums = null)
    {
        var checksums = versionChecksums != null && versionChecksums.Count > 0
            ? versionChecksums
            : _versionChecksums;

        var latestOfficialVersion = DetermineLatestVersion(checksums.Keys);

        var metadata = new ClientMetadata
        {
            GamePath = gamePath,
            TargetVersion = latestOfficialVersion,
            ExpectedSha256 = checksums.TryGetValue(latestOfficialVersion, out var targetSha) ? targetSha : null
        };

        if (string.IsNullOrWhiteSpace(gamePath))
        {
            metadata.Status = ClientStatus.GameNotFound;
            metadata.IsCorrectVersion = false;
            return metadata;
        }

        var resolvedExe = ResolveExePath(gamePath);
        metadata.ExePath = resolvedExe;

        if (string.IsNullOrEmpty(resolvedExe) || !File.Exists(resolvedExe))
        {
            metadata.Status = ClientStatus.GameNotFound;
            metadata.IsCorrectVersion = false;
            return metadata;
        }

        var resolvedAssembly = ResolveAssemblyPath(gamePath);
        metadata.AssemblyPath = resolvedAssembly;

        if (!string.IsNullOrEmpty(resolvedAssembly))
        {
            metadata.ManagedDirectory = Path.GetDirectoryName(resolvedAssembly);
        }

        if (string.IsNullOrEmpty(resolvedAssembly) || !File.Exists(resolvedAssembly))
        {
            metadata.Status = ClientStatus.AssemblyNotFound;
            metadata.IsCorrectVersion = false;
            return metadata;
        }

        try
        {
            var fileInfo = new FileInfo(resolvedAssembly);
            metadata.FileSizeBytes = fileInfo.Length;
            metadata.LastModifiedTime = fileInfo.LastWriteTime;

            var actualHash = ComputeFileSha256(resolvedAssembly);
            metadata.ActualSha256 = actualHash;

            string? matchedVersionBySha = null;
            foreach (var (ver, hash) in checksums)
            {
                if (string.Equals(hash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    matchedVersionBySha = ver;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(matchedVersionBySha))
            {
                metadata.DetectedVersion = matchedVersionBySha;
                metadata.IsShaMatch = true;
                metadata.ExpectedSha256 = checksums[matchedVersionBySha];

                bool isLatest = string.Equals(matchedVersionBySha, latestOfficialVersion, StringComparison.OrdinalIgnoreCase);
                metadata.IsCorrectVersion = isLatest;
                metadata.IsModified = false;
                metadata.Status = isLatest ? ClientStatus.OfficialVersionMatch : ClientStatus.OutdatedVersion;
            }
            else
            {
                metadata.IsShaMatch = false;
                metadata.IsModified = true;

                string? detectedVer = TryDetectVersionFromGame(resolvedExe, resolvedAssembly);
                metadata.DetectedVersion = detectedVer;

                if (!string.IsNullOrEmpty(detectedVer))
                {
                    metadata.ExpectedSha256 = checksums.TryGetValue(detectedVer, out var expVer)
                        ? expVer
                        : (checksums.TryGetValue(latestOfficialVersion, out var expLatest) ? expLatest : null);

                    bool isTarget = string.Equals(detectedVer, latestOfficialVersion, StringComparison.OrdinalIgnoreCase);
                    metadata.IsCorrectVersion = isTarget;

                    if (isTarget)
                    {
                        metadata.Status = ClientStatus.ModifiedOrMod;
                    }
                    else
                    {
                        metadata.Status = ClientStatus.OutdatedVersion;
                    }
                }
                else
                {
                    metadata.IsCorrectVersion = false;
                    metadata.ExpectedSha256 = checksums.TryGetValue(latestOfficialVersion, out var expLatest) ? expLatest : null;
                    metadata.Status = ClientStatus.ModifiedOrMod;
                }
            }
        }
        catch (Exception ex)
        {
            metadata.IsCorrectVersion = false;
            metadata.IsShaMatch = false;
            metadata.Status = ClientStatus.Error;
            metadata.ErrorMessage = ex.Message;
        }

        return metadata;
    }

    private static string DetermineLatestVersion(IEnumerable<string> versions)
    {
        Version? maxVer = null;
        string latest = Constants.Client.DefaultVersion;

        foreach (var v in versions)
        {
            var match = Regex.Match(v, @"^\d+(\.\d+)+");
            if (match.Success && Version.TryParse(match.Value, out var parsed))
            {
                if (maxVer == null || parsed > maxVer)
                {
                    maxVer = parsed;
                    latest = v;
                }
            }
            else if (maxVer == null)
            {
                latest = v;
            }
        }
        return latest;
    }

    private static string? TryDetectVersionFromGame(string exePath, string assemblyPath)
    {
        try
        {
            if (File.Exists(assemblyPath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(assemblyPath);
                var ver = ExtractVersionString(fvi.FileVersion) ?? ExtractVersionString(fvi.ProductVersion);
                if (!string.IsNullOrEmpty(ver)) return ver;
            }

            if (File.Exists(exePath))
            {
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                var ver = ExtractVersionString(fvi.FileVersion) ?? ExtractVersionString(fvi.ProductVersion);
                if (!string.IsNullOrEmpty(ver)) return ver;
            }

            var exeName = Path.GetFileNameWithoutExtension(exePath);
            var verFromName = ExtractVersionFromText(exeName);
            if (!string.IsNullOrEmpty(verFromName)) return verFromName;

            var dirName = Path.GetFileName(Path.GetDirectoryName(exePath) ?? "");
            var verFromDir = ExtractVersionFromText(dirName);
            if (!string.IsNullOrEmpty(verFromDir)) return verFromDir;
        }
        catch { }

        return null;
    }

    private static string? ExtractVersionString(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = Regex.Match(text, @"\b\d+\.\d+(\.\d+)?\b");
        return match.Success ? match.Value : null;
    }

    private static string? ExtractVersionFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var matchDot = Regex.Match(text, @"\b\d+\.\d+(\.\d+)?\b");
        if (matchDot.Success) return matchDot.Value;

        var matchDigits = Regex.Match(text, @"(?<!\d)(\d)(\d{2})(?!\d)");
        if (matchDigits.Success)
        {
            return $"{matchDigits.Groups[1].Value}.{matchDigits.Groups[2].Value[0]}.{matchDigits.Groups[2].Value[1]}";
        }

        return null;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
