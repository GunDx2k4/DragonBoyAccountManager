namespace DragonBoyAccountManager.Core.Interop.Schema;

public interface IVersionOffsetProvider
{
    Task<GameVersionSchema?> GetSchemaAsync(string? version = null, string? exeSha256 = null, CancellationToken cancellationToken = default);
    GameVersionSchema GetDefaultSchema(string? version = null);
}
