using DragonBoyAccountManager.Core.Game.Models;
using DragonBoyAccountManager.Core.Game.Modules;
using DragonBoyAccountManager.Core.Game.Snapshots;
using DragonBoyAccountManager.GUI.Models;

namespace DragonBoyAccountManager.GUI.Services.GameEngine;

public interface IGameEngineService
{
    Task<bool> StartAsync(Account account, string gamePath);
    Task StopAsync(Guid accountId);
    Task StopAllAsync();
    void StopAll();

    bool IsRunning(Guid accountId);
    int? GetProcessId(Guid accountId);
    bool FocusWindow(Guid accountId);
    void ArrangeWindows(IEnumerable<Guid>? accountIds = null);

    void SetGameSpeed(Guid accountId, float speed);
    float GetGameSpeed(Guid accountId);
    void SendGameKeyDown(Guid accountId, GameKey key);
    void SendGameKeyUp(Guid accountId, GameKey key);
    Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120);
    void SendGameKeyDown(Guid accountId, int keyCode);
    void SendGameKeyUp(Guid accountId, int keyCode);
    Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120);

    Task<bool> LoginAsync(Guid accountId);

    bool ChangeZone(Guid accountId, int zoneId);
    void RequestZoneList(Guid accountId);
    void CloseZonePanel(Guid accountId);
    bool MoveToWaypoint(Guid accountId, int targetX, int targetY);
    Task<bool> AutoMoveAsync(Guid accountId, int targetX, int targetY, bool isFastTeleport = false, CancellationToken cancellationToken = default);
    bool FocusOrClickObject(Guid accountId, string entityType, int entityId);
    WorldSnapshot? GetWorldSnapshot(Guid accountId);
    PlayerSnapshot? GetPlayerSnapshot(Guid accountId);

    bool SendChat(Guid accountId, string text, ChatType type = ChatType.Map);
    Task<bool> SendChatAsync(Guid accountId, string text, ChatType type = ChatType.Map, CancellationToken cancellationToken = default);
    void StartAutoChat(Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false);
    void StopAutoChat(Guid accountId);
    bool IsAutoChatRunning(Guid accountId);

    (bool Success, string Message) UseCapsule(Guid accountId);
    (bool Success, string Message) UseTdlt(Guid accountId);
    (bool Success, string Message) UseSkill(Guid accountId, int slotIndex);
    IReadOnlyList<GameSkillInfo> GetSkills(Guid accountId);

    event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? OnSessionUpdated;
    event Action<Guid, string>? OnSessionMessageUpdated;
    event Action<Guid, string, int>? OnChatSent;
}
