using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine.Abstractions;

public interface IGameEngine : IDisposable
{
    ISessionManager Sessions { get; }

    Task<bool> StartAccountAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default);
    Task StopAccountAsync(Guid accountId);
    Task StopAllAccountsAsync();
    void StopAllAccounts();

    bool IsRunning(Guid accountId);
    int? GetProcessId(Guid accountId);

    Task<bool> LoginAsync(Guid accountId);

    bool ChangeZone(Guid accountId, int zoneId);
    void RequestZoneList(Guid accountId);
    void CloseZonePanel(Guid accountId);

    bool MoveToWaypoint(Guid accountId, int targetX, int targetY);
    Task<bool> AutoMoveAsync(Guid accountId, int targetX, int targetY, bool isFastTeleport = false, CancellationToken cancellationToken = default);

    WorldSnapshot? GetWorldSnapshot(Guid accountId);
    PlayerSnapshot? GetPlayerSnapshot(Guid accountId);

    bool SendChat(Guid accountId, string text, ChatType type = ChatType.Map);
    Task<bool> SendChatAsync(Guid accountId, string text, ChatType type = ChatType.Map, CancellationToken cancellationToken = default);

    void StartAutoChat(Guid accountId, AutoChatOptions options);
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

    Task<Result> UseCapsuleAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<Result> UseTdltAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<Result> UsePeanAsync(Guid accountId, CancellationToken cancellationToken = default);
    Task<Result> SelectSkillAsync(Guid accountId, int slotIndex, CancellationToken cancellationToken = default);
    Task<Result> AutoAssignSkillsAsync(Guid accountId, CancellationToken cancellationToken = default);
    IReadOnlyList<GameSkillInfo> GetSkills(Guid accountId);

    void SetGameSpeed(Guid accountId, float speed);
    float GetGameSpeed(Guid accountId);

    void SendGameKeyDown(Guid accountId, GameKey key);
    void SendGameKeyUp(Guid accountId, GameKey key);
    Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120);

    void SendGameKeyDown(Guid accountId, int keyCode);
    void SendGameKeyUp(Guid accountId, int keyCode);
    Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120);

    event Action<SessionEvent>? OnSessionEvent;

    event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? OnSessionUpdated;
    event Action<Guid, string>? OnSessionMessageUpdated;
    event Action<Guid, ActionExecutionReport>? OnActionReported;
    event Action<Guid, string, int>? OnChatSent;
    event Action<Guid, string>? OnAutoTrainStatusChanged;
    event Action<Guid, bool>? OnAutoTrainStateChanged;
}
