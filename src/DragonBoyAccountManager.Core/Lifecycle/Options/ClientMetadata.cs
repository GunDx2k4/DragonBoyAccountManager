using System;
using System.IO;

namespace DragonBoyAccountManager.Core.Lifecycle.Options;

public record ResolvedGamePaths(string DataPath, string ManagedPath, string AssemblyPath = "");

public sealed class ClientMetadata
{
    public string? GamePath { get; set; }
    public string? ExePath { get; set; }
    public string? AssemblyPath { get; set; }
    public string? ManagedDirectory { get; set; }
    public bool IsFound => !string.IsNullOrEmpty(AssemblyPath) && File.Exists(AssemblyPath);
    public long FileSizeBytes { get; set; }
    public DateTime? LastModifiedTime { get; set; }
    public string ActualSha256 { get; set; } = string.Empty;
    public string? ExpectedSha256 { get; set; }
    public string TargetVersion { get; set; } = string.Empty;
    public string? DetectedVersion { get; set; }
    public bool IsCorrectVersion { get; set; }
    public bool IsShaMatch { get; set; }
    public bool IsModified { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Unknown;
    public string? ErrorMessage { get; set; }
}
