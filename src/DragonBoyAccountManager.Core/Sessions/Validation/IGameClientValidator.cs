namespace DragonBoyAccountManager.Core.Sessions.Validation;

public interface IGameClientValidator : IDisposable
{
    bool CheckIsValidGameExe(string? path);
    string? ResolveExePath(string? gamePath);
    string? ResolveAssemblyPath(string? gamePath);
    ResolvedGamePaths? ResolveGamePaths(string? gamePath);
    string ComputeFileSha256(string filePath);
    Task<Dictionary<string, string>> GetSha256ChecksumsAsync(CancellationToken cancellationToken = default);
    ClientMetadata InspectClientMetadata(string? gamePath, IReadOnlyDictionary<string, string>? versionChecksums = null);
}
