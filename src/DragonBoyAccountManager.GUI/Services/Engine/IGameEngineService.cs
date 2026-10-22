using DragonBoyAccountManager.Core.Engine.Abstractions;

namespace DragonBoyAccountManager.GUI.Services.Engine;

public interface ISessionManagementService
{
    Task<bool> StartAsync(Account account, string gamePath);
    Task StopAsync(Guid accountId);
    Task StopAllAsync();
    void StopAll();
    int? GetProcessId(Guid accountId);
    Task<bool> LoginAsync(Guid accountId);
}

public interface IGameWindowControlService
{
    bool FocusWindow(Guid accountId);
    void ArrangeWindows(IEnumerable<Guid>? accountIds = null);
    bool SetResolution(Guid accountId, int width, int height);
    void SetMasked(bool isMasked);
}

public interface IGameEngineService :
    ISessionManagementService,
    IGameWindowControlService,
    IGameActionService,
    IGameAutomationService,
    IGameEngineEvents
{
}
