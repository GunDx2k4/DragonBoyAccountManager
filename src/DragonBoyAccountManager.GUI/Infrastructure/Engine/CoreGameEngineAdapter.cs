using System.Collections.Concurrent;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.GUI.Infrastructure.Engine;

public sealed class CoreGameEngineAdapter : IGameEngineService, IDisposable
{
    private readonly IGameEngine _gameEngine;
    private readonly IWindowManagerService _windowManager;
    private readonly ISettingsService? _settingsService;
    private readonly ConcurrentDictionary<Guid, Account> _accounts = new();
    private bool _isMasked;

    public event Action<SessionEvent>? OnSessionEvent;
    public event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? OnSessionUpdated;
    public event Action<Guid, string>? OnSessionMessageUpdated;
    public event Action<Guid, ActionExecutionReport>? OnActionReported;
    public event Action<Guid, string, int>? OnChatSent;
    public event Action<Guid, string>? OnAutoTrainStatusChanged;
    public event Action<Guid, bool>? OnAutoTrainStateChanged;

    public CoreGameEngineAdapter(
        IGameEngine? gameEngine = null,
        ISettingsService? settingsService = null,
        IWindowManagerService? windowManager = null)
    {
        _gameEngine = gameEngine ?? new DragonBoyAccountManager.Core.Engine.GameEngine();
        _settingsService = settingsService;
        _windowManager = windowManager ?? new WindowManagerService();
        _isMasked = settingsService?.Current.IsMasked ?? false;

        _gameEngine.OnSessionEvent += evt => OnSessionEvent?.Invoke(evt);
        _gameEngine.OnSessionUpdated += HandleSessionUpdated;
        _gameEngine.OnSessionMessageUpdated += (id, msg) => OnSessionMessageUpdated?.Invoke(id, msg);
        _gameEngine.OnActionReported += (id, rep) => OnActionReported?.Invoke(id, rep);
        _gameEngine.OnChatSent += (id, msg, cnt) => OnChatSent?.Invoke(id, msg, cnt);
        _gameEngine.OnAutoTrainStatusChanged += (id, st) => OnAutoTrainStatusChanged?.Invoke(id, st);
        _gameEngine.OnAutoTrainStateChanged += (id, run) => OnAutoTrainStateChanged?.Invoke(id, run);

        _gameEngine.Sessions.SessionStarted += HandleSessionStarted;
    }

    private string FormatWindowTitle(string username, string serverName, string? charName)
    {
        string displayUser = _isMasked ? TextMaskHelper.MaskText(username) : username;
        string serverInfo = !string.IsNullOrWhiteSpace(serverName) ? $" ({serverName})" : string.Empty;
        if (!string.IsNullOrWhiteSpace(charName))
        {
            string displayChar = _isMasked ? TextMaskHelper.MaskText(charName) : charName;
            return $"DBAM - {displayUser}{serverInfo} [{displayChar}]";
        }
        return $"DBAM - {displayUser}{serverInfo}";
    }

    private void HandleSessionUpdated(Guid accountId, GameSessionStatus status, GameSessionSnapshot? snapshot)
    {
        OnSessionUpdated?.Invoke(accountId, status, snapshot);
        WeakReferenceMessenger.Default.Send(new SessionSnapshotUpdatedMessage(accountId, status, snapshot));

        if (status == GameSessionStatus.Active && snapshot?.Player != null && !string.IsNullOrWhiteSpace(snapshot.Player.Name))
        {
            var session = _gameEngine.Sessions.GetSession(accountId);
            if (session != null && session.IsRunning)
            {
                var account = _accounts.GetValueOrDefault(accountId);
                string username = account?.Username ?? string.Empty;
                string serverName = account?.ServerName ?? string.Empty;
                string title = FormatWindowTitle(username, serverName, snapshot.Player.Name);
                _windowManager.UpdateWindowTitle(session.Process, title);
            }
        }
    }

    private void HandleSessionStarted(Guid accountId, Process process)
    {
        int customW = _settingsService?.Current.WindowCustomWidth ?? 0;
        int customH = _settingsService?.Current.WindowCustomHeight ?? 0;
        var account = _accounts.GetValueOrDefault(accountId);
        string username = account?.Username ?? accountId.ToString();
        string serverName = account?.ServerName ?? string.Empty;

        _ = Task.Run(() => _windowManager.ApplyWindowStyleAsync(process, () =>
        {
            var snap = _gameEngine.GetPlayerSnapshot(accountId);
            string? charName = (snap != null && snap.IsInGame && !string.IsNullOrWhiteSpace(snap.Name)) ? snap.Name : null;
            return FormatWindowTitle(username, serverName, charName);
        }, customW, customH));
    }

    public async Task<bool> StartAsync(Account account, string gamePath)
    {
        _accounts[account.Id] = account;

        int customW = _settingsService?.Current.WindowCustomWidth ?? 0;
        int customH = _settingsService?.Current.WindowCustomHeight ?? 0;
        int width = customW > 0 ? customW : 1024;
        int height = customH > 0 ? customH : 600;

        var options = new AccountLaunchOptions
        {
            AccountId = account.Id,
            Username = account.Username,
            Password = account.Password,
            ServerId = account.ServerId,
            ServerName = account.ServerName,
            GamePath = gamePath,
            ScreenWidth = width,
            ScreenHeight = height,
            GameSpeed = account.GameSpeed,
            AutoLogin = account.AutoLogin,
            AutoAssignSkills = _settingsService?.Current.AutoAssignSkills == true,
            IsMasked = _isMasked
        };

        bool started = await _gameEngine.StartAccountAsync(options);

        if (started && _settingsService?.Current.AutoArrangeWindows == true)
        {
            ArrangeWindows();
        }

        return started;
    }

    public Task StopAsync(Guid accountId)
    {
        _accounts.TryRemove(accountId, out _);
        return _gameEngine.StopAccountAsync(accountId);
    }

    public Task StopAllAsync()
    {
        _accounts.Clear();
        return _gameEngine.StopAllAccountsAsync();
    }

    public void StopAll()
    {
        _accounts.Clear();
        _gameEngine.StopAllAccounts();
    }

    public bool IsRunning(Guid accountId) => _gameEngine.IsRunning(accountId);

    public int? GetProcessId(Guid accountId) => _gameEngine.GetProcessId(accountId);

    public bool FocusWindow(Guid accountId)
    {
        var session = _gameEngine.Sessions.GetSession(accountId);
        return session != null && _windowManager.BringToFront(session.Process);
    }

    public void ArrangeWindows(IEnumerable<Guid>? accountIds = null)
    {
        var targetIds = accountIds?.ToList() ?? _gameEngine.Sessions.ActiveSessionIds.ToList();
        var processes = new List<Process>();

        foreach (var id in targetIds)
        {
            var session = _gameEngine.Sessions.GetSession(id);
            if (session != null && session.IsRunning)
            {
                processes.Add(session.Process);
            }
        }

        if (processes.Count == 0) return;

        var mode = _settingsService?.Current.WindowArrangeMode ?? WindowArrangeMode.Grid;
        var options = new WindowArrangeOptions
        {
            Mode = mode,
            Columns = _settingsService?.Current.WindowArrangeColumns ?? 0
        };

        _windowManager.ArrangeWindows(processes, options);
    }

    public bool SetResolution(Guid accountId, int width, int height)
    {
        var session = _gameEngine.Sessions.GetSession(accountId);
        return session != null && _windowManager.ResizeWindow(session.Process, width, height);
    }

    public void SetMasked(bool isMasked)
    {
        _isMasked = isMasked;
        _gameEngine.SetAllMasked(isMasked);

        foreach (var accountId in _gameEngine.Sessions.ActiveSessionIds)
        {
            var session = _gameEngine.Sessions.GetSession(accountId);
            if (session != null && session.IsRunning)
            {
                var account = _accounts.GetValueOrDefault(accountId);
                string username = account?.Username ?? string.Empty;
                string serverName = account?.ServerName ?? string.Empty;
                var snap = _gameEngine.GetPlayerSnapshot(accountId);
                string? charName = (snap != null && snap.IsInGame && !string.IsNullOrWhiteSpace(snap.Name)) ? snap.Name : null;
                string title = FormatWindowTitle(username, serverName, charName);
                _windowManager.UpdateWindowTitle(session.Process, title);
            }
        }
    }

    public void SetGameSpeed(Guid accountId, float speed)
    {
        if (_accounts.TryGetValue(accountId, out var account))
        {
            account.GameSpeed = speed;
        }
        _gameEngine.SetGameSpeed(accountId, speed);
    }

    public float GetGameSpeed(Guid accountId) => _gameEngine.GetGameSpeed(accountId);

    public void SendGameKeyDown(Guid accountId, GameKey key) => _gameEngine.SendGameKeyDown(accountId, key);
    public void SendGameKeyUp(Guid accountId, GameKey key) => _gameEngine.SendGameKeyUp(accountId, key);
    public Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120) => _gameEngine.SendGameKeyPressAsync(accountId, key, holdMs);

    public void SendGameKeyDown(Guid accountId, int keyCode) => _gameEngine.SendGameKeyDown(accountId, keyCode);
    public void SendGameKeyUp(Guid accountId, int keyCode) => _gameEngine.SendGameKeyUp(accountId, keyCode);
    public Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120) => _gameEngine.SendGameKeyPressAsync(accountId, keyCode, holdMs);

    public Task<bool> LoginAsync(Guid accountId) => _gameEngine.LoginAsync(accountId);

    public bool ChangeZone(Guid accountId, int zoneId) => _gameEngine.ChangeZone(accountId, zoneId);
    public void RequestZoneList(Guid accountId) => _gameEngine.RequestZoneList(accountId);
    public void CloseZonePanel(Guid accountId) => _gameEngine.CloseZonePanel(accountId);

    public bool MoveToWaypoint(Guid accountId, int targetX, int targetY) => _gameEngine.MoveToWaypoint(accountId, targetX, targetY);
    public Task<bool> AutoMoveAsync(Guid accountId, int targetX, int targetY, bool isFastTeleport = false, CancellationToken cancellationToken = default) =>
        _gameEngine.AutoMoveAsync(accountId, targetX, targetY, isFastTeleport, cancellationToken);

    public bool FocusOrClickObject(Guid accountId, string entityType, int entityId)
    {
        _gameEngine.Sessions.GetWorker(accountId)?.PublishSessionUpdate();
        return true;
    }

    public WorldSnapshot? GetWorldSnapshot(Guid accountId) => _gameEngine.GetWorldSnapshot(accountId);
    public PlayerSnapshot? GetPlayerSnapshot(Guid accountId) => _gameEngine.GetPlayerSnapshot(accountId);

    public bool SendChat(Guid accountId, string text, ChatType type = ChatType.Map) => _gameEngine.SendChat(accountId, text, type);
    public Task<bool> SendChatAsync(Guid accountId, string text, ChatType type = ChatType.Map, CancellationToken cancellationToken = default) =>
        _gameEngine.SendChatAsync(accountId, text, type, cancellationToken);

    public void StartAutoChat(Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false)
    {
        var options = new AutoChatOptions
        {
            Messages = messages,
            Delay = interval,
            IsGlobal = type == ChatType.Global,
            IsRandom = isRandom
        };
        _gameEngine.StartAutoChat(accountId, options);
    }

    public void UpdateAutoChatConfig(Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false)
    {
        var options = new AutoChatOptions
        {
            Messages = messages,
            Delay = interval,
            IsGlobal = type == ChatType.Global,
            IsRandom = isRandom
        };
        _gameEngine.StartAutoChat(accountId, options);
    }

    public void StopAutoChat(Guid accountId) => _gameEngine.StopAutoChat(accountId);
    public bool IsAutoChatRunning(Guid accountId) => _gameEngine.IsAutoChatRunning(accountId);

    public Task<Result> UseCapsuleAsync(Guid accountId, CancellationToken cancellationToken = default) => _gameEngine.UseCapsuleAsync(accountId, cancellationToken);
    public Task<Result> UseTdltAsync(Guid accountId, CancellationToken cancellationToken = default) => _gameEngine.UseTdltAsync(accountId, cancellationToken);
    public Task<Result> UsePeanAsync(Guid accountId, CancellationToken cancellationToken = default) => _gameEngine.UsePeanAsync(accountId, cancellationToken);
    public Task<Result> SelectSkillAsync(Guid accountId, int slotIndex, CancellationToken cancellationToken = default) => _gameEngine.SelectSkillAsync(accountId, slotIndex, cancellationToken);

    public IReadOnlyList<GameSkillInfo> GetSkills(Guid accountId) => _gameEngine.GetSkills(accountId);
    public Task<Result> AutoAssignSkillsAsync(Guid accountId, CancellationToken cancellationToken = default) => _gameEngine.AutoAssignSkillsAsync(accountId, cancellationToken);

    public void StartAutoTrain(Guid accountId, AutoTrainOptions options) => _gameEngine.StartAutoTrain(accountId, options);
    public void UpdateAutoTrainOptions(Guid accountId, AutoTrainOptions options) => _gameEngine.UpdateAutoTrainOptions(accountId, options);
    public void StopAutoTrain(Guid accountId) => _gameEngine.StopAutoTrain(accountId);
    public bool IsAutoTrainRunning(Guid accountId) => _gameEngine.IsAutoTrainRunning(accountId);

    public bool StartBehavior(Guid accountId, string behaviorId) => _gameEngine.StartBehavior(accountId, behaviorId);
    public bool StopBehavior(Guid accountId, string behaviorId) => _gameEngine.StopBehavior(accountId, behaviorId);
    public bool IsBehaviorRunning(Guid accountId, string behaviorId) => _gameEngine.IsBehaviorRunning(accountId, behaviorId);
    public IReadOnlyCollection<IBehavior> GetBehaviors(Guid accountId) => _gameEngine.GetBehaviors(accountId);

    public void Dispose()
    {
        StopAll();
        _gameEngine.Dispose();
        GC.SuppressFinalize(this);
    }
}
