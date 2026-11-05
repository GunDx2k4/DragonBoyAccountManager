using System.Text.Json.Serialization;

namespace DragonBoyAccountManager.Core.Interop.Schema;

public sealed class GameVersionSchema
{
    [JsonPropertyName("Version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("ExeSha256")]
    public string ExeSha256 { get; set; } = string.Empty;

    [JsonPropertyName("StaticFieldsOffset")]
    public int StaticFieldsOffset { get; set; } = 184;

    [JsonPropertyName("TypeInfos")]
    public Dictionary<string, long> TypeInfos { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("StaticFields")]
    public Dictionary<string, int> StaticFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("InstanceFields")]
    public Dictionary<string, int> InstanceFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("Methods")]
    public Dictionary<string, long> Methods { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public long GetTypeInfoRva(string className)
    {
        return TypeInfos.TryGetValue(className, out var rva) ? rva : 0;
    }

    public int GetStaticFieldOffset(string className, string fieldName)
    {
        return StaticFields.TryGetValue($"{className}.{fieldName}", out var offset) ? offset : -1;
    }

    public int GetInstanceFieldOffset(string className, string fieldName)
    {
        return InstanceFields.TryGetValue($"{className}.{fieldName}", out var offset) ? offset : -1;
    }

    public long GetMethodRva(string className, string methodName)
    {
        return Methods.TryGetValue($"{className}.{methodName}", out var rva) ? rva : 0;
    }
}
