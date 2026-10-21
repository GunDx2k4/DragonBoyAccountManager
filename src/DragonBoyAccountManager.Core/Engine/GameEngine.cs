using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine;

public sealed class GameEngine : IGameEngine
{
    public ISessionManager Sessions { get; }

    public event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? OnSessionUpdated;
    public event Action<Guid, string>? OnSessionMessageUpdated;
    public event Action<Guid, ActionExecutionReport>? OnActionReported;
    public event Action<Guid, string, int>? OnChatSent;
    public event Action<Guid, string>? OnAutoTrainStatusChanged;
    public event Action<Guid, bool>? OnAutoTrainStateChanged;

    public GameEngine(ISessionManager? sessionManager = null)
    {
        Sessions = sessionManager ?? new SessionManager();

        Sessions.SessionUpdated += (id, status, snap) => OnSessionUpdated?.Invoke(id, status, snap);
        Sessions.MessageUpdated += (id, msg) => OnSessionMessageUpdated?.Invoke(id, msg);
        Sessions.ActionReported += (id, rep) => OnActionReported?.Invoke(id, rep);
        Sessions.ChatSent += (id, msg, cnt) => OnChatSent?.Invoke(id, msg, cnt);
        Sessions.AutoTrainStatusChanged += (id, st) => OnAutoTrainStatusChanged?.Invoke(id, st);
        Sessions.AutoTrainStateChanged += (id, run) => OnAutoTrainStateChanged?.Invoke(id, run);
    }

    public Task<bool> StartAccountAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default)
    {
        return Sessions.StartSessionAsync(options, cancellationToken);
    }

    public Task StopAccountAsync(Guid accountId)
    {
        return Sessions.StopSessionAsync(accountId);
    }

    public Task StopAllAccountsAsync()
    {
        return Sessions.StopAllSessionsAsync();
    }

    public void StopAllAccounts()
    {
        Sessions.StopAllSessions();
    }

    public bool IsRunning(Guid accountId) => Sessions.IsRunning(accountId);

    public int? GetProcessId(Guid accountId) => Sessions.GetProcessId(accountId);

    public async Task<bool> LoginAsync(Guid accountId)
    {
        var worker = Sessions.GetWorker(accountId);
        if (worker != null)
        {
            return await worker.ExecuteLoginAsync();
        }
        return false;
    }

    public bool ChangeZone(Guid accountId, int zoneId)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            _ = session.Dispatcher.DispatchAsync(new ChangeZoneCommand(zoneId));
            return true;
        }
        return false;
    }

    public void RequestZoneList(Guid accountId)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameZoneController.RequestZoneList(session.Mono);
        }
    }

    public void CloseZonePanel(Guid accountId)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameZoneController.CloseZonePanel(session.Mono);
        }
    }

    public bool MoveToWaypoint(Guid accountId, int targetX, int targetY)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            _ = session.Dispatcher.DispatchAsync(new MoveCommand(targetX, targetY, false));
            Sessions.GetWorker(accountId)?.PublishSessionUpdate();
            return true;
        }
        return false;
    }

    public async Task<bool> AutoMoveAsync(Guid accountId, int targetX, int targetY, bool isFastTeleport = false, CancellationToken cancellationToken = default)
    {
        var worker = Sessions.GetWorker(accountId);
        if (worker != null && worker.Session.IsRunning && worker.Session.CurrentPlayer.IsInGame)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(worker.Cts.Token, cancellationToken);
            var result = await worker.Session.Navigator.NavigateAsync(
                worker.Session.CurrentPlayer,
                worker.Session.CurrentMap,
                targetX,
                targetY,
                isFastTeleport,
                null,
                linkedCts.Token);

            worker.PublishSessionUpdate();
            return result.IsSuccess;
        }
        return false;
    }

    public WorldSnapshot? GetWorldSnapshot(Guid accountId)
    {
        var session = Sessions.GetSession(accountId);
        return session != null && session.IsRunning ? session.CurrentWorld : null;
    }

    public PlayerSnapshot? GetPlayerSnapshot(Guid accountId)
    {
        var session = Sessions.GetSession(accountId);
        return session != null && session.IsRunning ? session.CurrentPlayer : null;
    }

    public bool SendChat(Guid accountId, string text, ChatType type = ChatType.Map)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            _ = session.Dispatcher.DispatchAsync(new SendChatCommand(text, type == ChatType.Global));
            return true;
        }
        return false;
    }

    public async Task<bool> SendChatAsync(Guid accountId, string text, ChatType type = ChatType.Map, CancellationToken cancellationToken = default)
    {
        var worker = Sessions.GetWorker(accountId);
        if (worker != null && worker.Session.IsRunning)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(worker.Cts.Token, cancellationToken);
            var result = await worker.Session.Dispatcher.DispatchAsync(new SendChatCommand(text, type == ChatType.Global), linkedCts.Token);
            return result.IsSuccess;
        }
        return false;
    }

    public void StartAutoChat(Guid accountId, AutoChatOptions options)
    {
        Sessions.GetWorker(accountId)?.StartAutoChat(options);
    }

    public void StopAutoChat(Guid accountId)
    {
        Sessions.GetWorker(accountId)?.StopAutoChat();
    }

    public bool IsAutoChatRunning(Guid accountId)
    {
        var worker = Sessions.GetWorker(accountId);
        return worker != null && worker.IsAutoChatRunning;
    }

    public void StartAutoTrain(Guid accountId, AutoTrainOptions options)
    {
        Sessions.GetWorker(accountId)?.StartAutoTrain(options);
    }

    public void UpdateAutoTrainOptions(Guid accountId, AutoTrainOptions options)
    {
        var worker = Sessions.GetWorker(accountId);
        if (worker != null && worker.Session.IsRunning)
        {
            worker.Session.TrainBehavior.UpdateOptions(options);
        }
    }

    public void StopAutoTrain(Guid accountId)
    {
        Sessions.GetWorker(accountId)?.StopAutoTrain();
    }

    public bool IsAutoTrainRunning(Guid accountId)
    {
        var worker = Sessions.GetWorker(accountId);
        return worker != null && worker.IsAutoTrainRunning;
    }

    public bool StartBehavior(Guid accountId, string behaviorId)
        => Sessions.StartBehavior(accountId, behaviorId);

    public bool StopBehavior(Guid accountId, string behaviorId)
        => Sessions.StopBehavior(accountId, behaviorId);

    public bool IsBehaviorRunning(Guid accountId, string behaviorId)
        => Sessions.IsBehaviorRunning(accountId, behaviorId);

    public IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId)
        => Sessions.GetBehaviors(accountId);

    public Result UseCapsule(Guid accountId)
        => UseCapsuleAsync(accountId).GetAwaiter().GetResult();

    public async Task<Result> UseCapsuleAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            var res = await session.Dispatcher.DispatchAsync(new UseItemCommand(-1, 193, "Capsule"), cancellationToken);
            var report = res.IsSuccess
                ? ActionExecutionReport.Success("Sử dụng Capsule", res.Message)
                : ActionExecutionReport.Failure("Sử dụng Capsule", res.Message);
            OnActionReported?.Invoke(accountId, report);
            return res;
        }
        return Result.Failure("Tài khoản chưa vào game.");
    }

    public Result UseTdlt(Guid accountId)
        => UseTdltAsync(accountId).GetAwaiter().GetResult();

    public async Task<Result> UseTdltAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            var res = await session.Dispatcher.DispatchAsync(new UseItemCommand(-1, 521, "TDLT"), cancellationToken);
            var report = res.IsSuccess
                ? ActionExecutionReport.Success("Sử dụng TDLT", res.Message)
                : ActionExecutionReport.Failure("Sử dụng TDLT", res.Message);
            OnActionReported?.Invoke(accountId, report);
            return res;
        }
        return Result.Failure("Tài khoản chưa vào game.");
    }

    public Result UsePean(Guid accountId)
        => UsePeanAsync(accountId).GetAwaiter().GetResult();

    public async Task<Result> UsePeanAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            var res = await session.Dispatcher.DispatchAsync(new UseItemCommand(-1, 13, "Đậu thần"), cancellationToken);
            var report = res.IsSuccess
                ? ActionExecutionReport.Success("Sử dụng Đậu thần", res.Message)
                : ActionExecutionReport.Failure("Sử dụng Đậu thần", res.Message);
            OnActionReported?.Invoke(accountId, report);
            return res;
        }
        return Result.Failure("Tài khoản chưa vào game.");
    }

    public Result SelectSkill(Guid accountId, int slotIndex)
        => SelectSkillAsync(accountId, slotIndex).GetAwaiter().GetResult();

    public async Task<Result> SelectSkillAsync(Guid accountId, int slotIndex, CancellationToken cancellationToken = default)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            var res = await session.Dispatcher.DispatchAsync(new SelectSkillCommand(slotIndex), cancellationToken);
            var report = res.IsSuccess
                ? ActionExecutionReport.Success("Chọn kỹ năng", res.Message)
                : ActionExecutionReport.Failure("Chọn kỹ năng", res.Message);
            OnActionReported?.Invoke(accountId, report);
            return res;
        }
        return Result.Failure("Tài khoản chưa vào game.");
    }

    public Result AutoAssignSkills(Guid accountId)
        => AutoAssignSkillsAsync(accountId).GetAwaiter().GetResult();

    public async Task<Result> AutoAssignSkillsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var worker = Sessions.GetWorker(accountId);
        if (worker != null)
        {
            return await worker.AutoAssignSkillsAsync(cancellationToken);
        }
        return Result.Failure("Tài khoản chưa vào game.");
    }

    public IReadOnlyList<GameSkillInfo> GetSkills(Guid accountId)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            return session.PlayerScraper.ScrapeSkills();
        }
        return [];
    }

    public void SetGameSpeed(Guid accountId, float speed)
    {
        Sessions.GetWorker(accountId)?.SetGameSpeed(speed);
    }

    public float GetGameSpeed(Guid accountId)
    {
        var worker = Sessions.GetWorker(accountId);
        return worker?.GameSpeed ?? 1.0f;
    }

    public void SendGameKeyDown(Guid accountId, GameKey key)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyDown(session.Mono, key);
        }
    }

    public void SendGameKeyUp(Guid accountId, GameKey key)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyUp(session.Mono, key);
        }
    }

    public Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            return GameInputSender.SendKeyPressAsync(session.Mono, key, holdMs);
        }
        return Task.CompletedTask;
    }

    public void SendGameKeyDown(Guid accountId, int keyCode)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyDown(session.Mono, keyCode);
        }
    }

    public void SendGameKeyUp(Guid accountId, int keyCode)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyUp(session.Mono, keyCode);
        }
    }

    public Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            return GameInputSender.SendKeyPressAsync(session.Mono, keyCode, holdMs);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        StopAllAccounts();
        if (Sessions is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
