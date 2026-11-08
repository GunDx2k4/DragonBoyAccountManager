namespace DragonBoyAccountManager.GUI.Services.Engine;

public interface IGameWindowControlService
{
    bool FocusWindow(Guid accountId);
    void ArrangeWindows(IEnumerable<Guid>? accountIds = null);
    bool SetResolution(Guid accountId, int width, int height);
    void SetMasked(bool isMasked);
}
