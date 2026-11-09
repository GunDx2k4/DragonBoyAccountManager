namespace DragonBoyAccountManager.GUI.Services.Engine;

public interface ISessionManagementService
{
    Task<bool> StartAsync(Account account, string gamePath);
    Task StopAsync(Guid accountId);
    Task StopAllAsync();
    void StopAll();
    int? GetProcessId(Guid accountId);
    Task<bool> LoginAsync(Guid accountId);
    IReadOnlyCollection<Guid> ActiveSessionIds { get; }
}
