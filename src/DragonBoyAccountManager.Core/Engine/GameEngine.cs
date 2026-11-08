using System.Diagnostics;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Engine;

public sealed class GameEngine : IGameEngine, IAsyncDisposable
{
    public ISessionManager Sessions { get; }

    public event Action<SessionEvent>? OnSessionEvent;

    public GameEngine(ISessionManager? sessionManager = null)
    {
        Sessions = sessionManager ?? new SessionManager();
        Sessions.EventPublished += evt => OnSessionEvent?.Invoke(evt);
    }

    public Task<bool> StartAccountAsync(AccountLaunchOptions options, CancellationToken cancellationToken = default)
        => Sessions.StartSessionAsync(options, cancellationToken);

    public Task StopAccountAsync(Guid accountId)
        => Sessions.StopSessionAsync(accountId);

    public Task StopAllAccountsAsync()
        => Sessions.StopAllSessionsAsync();

    public void StopAllAccounts()
        => Sessions.StopAllSessions();

    public bool IsRunning(Guid accountId) => Sessions.IsRunning(accountId);

    public int? GetProcessId(Guid accountId) => Sessions.GetProcessId(accountId);

    public Process? GetProcess(Guid accountId) => Sessions.GetProcess(accountId);

    public async Task<Result> ExecuteCommandAsync(Guid accountId, IGameCommand command, CancellationToken cancellationToken = default)
    {
        var worker = Sessions.GetWorker(accountId);
        return worker != null
            ? await worker.ExecuteCommandAsync(command, cancellationToken)
            : Result.Failure("Tài khoản chưa vào game.");
    }

    public async Task<bool> LoginAsync(Guid accountId)
    {
        var worker = Sessions.GetWorker(accountId);
        return worker != null && await worker.ExecuteLoginAsync();
    }

    public TSnapshot? GetSnapshot<TSnapshot>(Guid accountId) where TSnapshot : class
        => Sessions.GetSnapshot<TSnapshot>(accountId);

    public bool StartBehavior<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class
        => Sessions.GetWorker(accountId)?.StartBehavior<TBehavior, TOptions>(options) ?? false;

    public bool UpdateBehaviorOptions<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class
        => Sessions.GetWorker(accountId)?.UpdateBehaviorOptions<TBehavior, TOptions>(options) ?? false;

    public bool StopBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
        => Sessions.GetWorker(accountId)?.StopBehavior<TBehavior>() ?? false;

    public bool IsBehaviorRunning<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
        => Sessions.GetWorker(accountId)?.IsBehaviorRunning<TBehavior>() ?? false;

    public TBehavior? GetBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior
        => Sessions.GetWorker(accountId)?.Session.GetBehavior<TBehavior>();

    public bool StartBehavior(Guid accountId, string behaviorId)
        => Sessions.GetWorker(accountId)?.StartBehavior(behaviorId) ?? false;

    public bool StopBehavior(Guid accountId, string behaviorId)
        => Sessions.GetWorker(accountId)?.StopBehavior(behaviorId) ?? false;

    public bool IsBehaviorRunning(Guid accountId, string behaviorId)
        => Sessions.GetWorker(accountId)?.IsBehaviorRunning(behaviorId) ?? false;

    public IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId)
        => Sessions.GetWorker(accountId)?.GetBehaviors() ?? [];

    public void SendGameKeyDown(Guid accountId, GameKey key)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyDown(session.Runtime, key);
        }
    }

    public void SendGameKeyUp(Guid accountId, GameKey key)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyUp(session.Runtime, key);
        }
    }

    public Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120)
    {
        var session = Sessions.GetSession(accountId);
        return session != null && session.IsRunning
            ? GameInputSender.SendKeyPressAsync(session.Runtime, key, holdMs)
            : Task.CompletedTask;
    }

    public void SendGameKeyDown(Guid accountId, int keyCode)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyDown(session.Runtime, keyCode);
        }
    }

    public void SendGameKeyUp(Guid accountId, int keyCode)
    {
        var session = Sessions.GetSession(accountId);
        if (session != null && session.IsRunning)
        {
            GameInputSender.SendKeyUp(session.Runtime, keyCode);
        }
    }

    public Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120)
    {
        var session = Sessions.GetSession(accountId);
        return session != null && session.IsRunning
            ? GameInputSender.SendKeyPressAsync(session.Runtime, keyCode, holdMs)
            : Task.CompletedTask;
    }

    public void SetMasked(Guid accountId, bool isMasked)
        => Sessions.GetWorker(accountId)?.SetMasked(isMasked);

    public void SetAllMasked(bool isMasked)
    {
        foreach (var id in Sessions.ActiveSessionIds)
        {
            Sessions.GetWorker(id)?.SetMasked(isMasked);
        }
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

    public async ValueTask DisposeAsync()
    {
        await StopAllAccountsAsync();
        if (Sessions is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (Sessions is IDisposable disposable)
        {
            disposable.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
