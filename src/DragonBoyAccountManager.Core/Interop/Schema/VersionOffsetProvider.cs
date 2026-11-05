using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using DragonBoyAccountManager.Core.Common;

namespace DragonBoyAccountManager.Core.Interop.Schema;

public class VersionOffsetProvider : IVersionOffsetProvider, IDisposable
{
    private static readonly Lazy<VersionOffsetProvider> _instance = new(() => new VersionOffsetProvider());
    public static VersionOffsetProvider Instance => _instance.Value;

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly ConcurrentDictionary<string, GameVersionSchema> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _diskCacheFilePath;
    private bool _embeddedLoaded;

    public VersionOffsetProvider(HttpClient? httpClient = null)
    {
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            _ownsHttpClient = true;
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _diskCacheFilePath = Path.Combine(localAppData, "DBAM", "schemas", "offsets.json");
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    public GameVersionSchema GetDefaultSchema(string? version = null)
    {
        string targetVersion = string.IsNullOrWhiteSpace(version) ? Constants.Client.DefaultVersion : version;

        if (_cache.TryGetValue(targetVersion, out var cached))
            return cached;

        LoadFromEmbeddedResource();

        if (_cache.TryGetValue(targetVersion, out var loaded))
            return loaded;

        var fallback = new GameVersionSchema { Version = targetVersion, StaticFieldsOffset = 184 };
        _cache[targetVersion] = fallback;
        return fallback;
    }

    public async Task<GameVersionSchema?> GetSchemaAsync(
        string? version = null,
        string? exeSha256 = null,
        CancellationToken cancellationToken = default)
    {
        string? targetVersion = string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(exeSha256)
            ? Constants.Client.DefaultVersion
            : version;

        var schema = FindInCache(targetVersion, exeSha256);
        if (schema != null)
            return schema;

        schema = await LoadFromDiskCacheAsync(targetVersion, exeSha256, cancellationToken);
        if (schema != null)
            return schema;

        LoadFromEmbeddedResource();
        schema = FindInCache(targetVersion, exeSha256);
        if (schema != null)
            return schema;

        schema = await LoadFromRemoteAsync(targetVersion, exeSha256, cancellationToken);
        if (schema != null)
            return schema;

        return GetDefaultSchema(targetVersion);
    }

    private GameVersionSchema? FindInCache(string? version, string? exeSha256)
    {
        if (!string.IsNullOrWhiteSpace(version) && _cache.TryGetValue(version, out var schemaByVer))
            return schemaByVer;

        if (!string.IsNullOrWhiteSpace(exeSha256) && _cache.TryGetValue(exeSha256, out var schemaBySha))
            return schemaBySha;

        return null;
    }

    private void PopulateCache(Dictionary<string, GameVersionSchema> schemas)
    {
        foreach (var (key, schema) in schemas)
        {
            if (string.IsNullOrEmpty(schema.Version))
            {
                schema.Version = key;
            }

            _cache[key] = schema;
            _cache[schema.Version] = schema;

            if (!string.IsNullOrWhiteSpace(schema.ExeSha256))
            {
                _cache[schema.ExeSha256] = schema;
            }
        }
    }

    private async Task<GameVersionSchema?> LoadFromDiskCacheAsync(string? version, string? exeSha256, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_diskCacheFilePath))
                return null;

            using var stream = File.OpenRead(_diskCacheFilePath);
            var schemas = await JsonSerializer.DeserializeAsync<Dictionary<string, GameVersionSchema>>(stream, cancellationToken: cancellationToken);
            if (schemas != null)
            {
                PopulateCache(schemas);
                return FindInCache(version, exeSha256);
            }
        }
        catch
        {
        }

        return null;
    }

    private async Task<GameVersionSchema?> LoadFromRemoteAsync(string? version, string? exeSha256, CancellationToken cancellationToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, Constants.Client.DefaultOffsetsUrl);
            var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                var schemas = JsonSerializer.Deserialize<Dictionary<string, GameVersionSchema>>(json);
                if (schemas != null && schemas.Count > 0)
                {
                    PopulateCache(schemas);

                    try
                    {
                        string? dir = Path.GetDirectoryName(_diskCacheFilePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        await File.WriteAllTextAsync(_diskCacheFilePath, json, cancellationToken);
                    }
                    catch
                    {
                    }

                    return FindInCache(version, exeSha256);
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private void LoadFromEmbeddedResource()
    {
        if (_embeddedLoaded) return;

        try
        {
            var assembly = typeof(VersionOffsetProvider).Assembly;
            using var stream = assembly.GetManifestResourceStream("DragonBoyAccountManager.Core.Resources.Offsets.json");
            if (stream != null)
            {
                var schemas = JsonSerializer.Deserialize<Dictionary<string, GameVersionSchema>>(stream);
                if (schemas != null)
                {
                    PopulateCache(schemas);
                    _embeddedLoaded = true;
                }
            }
        }
        catch
        {
        }
    }
}
