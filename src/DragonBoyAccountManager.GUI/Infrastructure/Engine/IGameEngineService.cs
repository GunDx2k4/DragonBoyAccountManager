using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.GUI.Infrastructure.Engine;

public interface ISessionManagementService
{
    Task<bool> StartAsync(Account account, string gamePath);
    Task StopAsync(Guid accountId);
    Task StopAllAsync();
    void StopAll();
    bool IsRunning(Guid accountId);
    int? GetProcessId(Guid accountId);
    Task<bool> LoginAsync(Guid accountId);
}

public interface IGameWindowControlService
{
    bool FocusWindow(Guid accountId);
    void ArrangeWindows(IEnumerable<Guid>? accountIds = null);
    bool SetResolution(Guid accountId, int width, int height);
}

public interface IGameActionService
{
    void SetGameSpeed(Guid accountId, float speed);
    float GetGameSpeed(Guid accountId);
    void SendGameKeyDown(Guid accountId, GameKey key);
    void SendGameKeyUp(Guid accountId, GameKey key);
    Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120);
    void SendGameKeyDown(Guid accountId, int keyCode);
    void SendGameKeyUp(Guid accountId, int keyCode);
    Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120);

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

    Result UseCapsule(Guid accountId);
    Task<Result> UseCapsuleAsync(Guid accountId, CancellationToken cancellationToken = default);
    Result UseTdlt(Guid accountId);
    Task<Result> UseTdltAsync(Guid accountId, CancellationToken cancellationToken = default);
    Result UsePean(Guid accountId);
    Task<Result> UsePeanAsync(Guid accountId, CancellationToken cancellationToken = default);
    Result SelectSkill(Guid accountId, int slotIndex);
    Task<Result> SelectSkillAsync(Guid accountId, int slotIndex, CancellationToken cancellationToken = default);
    Result UseSkill(Guid accountId, int slotIndex);
    IReadOnlyList<GameSkillInfo> GetSkills(Guid accountId);
    Result AutoAssignSkills(Guid accountId);
    Task<Result> AutoAssignSkillsAsync(Guid accountId, CancellationToken cancellationToken = default);
}

public interface IGameAutomationService
{
    void StartAutoChat(Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false);
    void UpdateAutoChatConfig(Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false);
    void StopAutoChat(Guid accountId);
    bool IsAutoChatRunning(Guid accountId);

    void StartAutoTrain(Guid accountId, AutoTrainOptions options);
    void UpdateAutoTrainOptions(Guid accountId, AutoTrainOptions options);
    void StopAutoTrain(Guid accountId);
    bool IsAutoTrainRunning(Guid accountId);

    bool StartBehavior(Guid accountId, string behaviorId);
    bool StopBehavior(Guid accountId, string behaviorId);
    bool IsBehaviorRunning(Guid accountId, string behaviorId);
    IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId);
}

public interface IGameEngineEvents
{
    event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? OnSessionUpdated;
    event Action<Guid, string>? OnSessionMessageUpdated;
    event Action<Guid, ActionExecutionReport>? OnActionReported;
    event Action<Guid, string, int>? OnChatSent;
    event Action<Guid, string>? OnAutoTrainStatusChanged;
    event Action<Guid, bool>? OnAutoTrainStateChanged;
}

public interface IGameEngineService :
    ISessionManagementService,
    IGameWindowControlService,
    IGameActionService,
    IGameAutomationService,
    IGameEngineEvents
{
}
