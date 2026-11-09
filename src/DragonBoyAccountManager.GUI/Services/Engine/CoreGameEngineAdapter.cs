using System.Collections.Concurrent;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.Core.Automation.Behaviors;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Engine;
using DragonBoyAccountManager.Core.Engine.Abstractions;
using DragonBoyAccountManager.Core.Engine.Events;
using DragonBoyAccountManager.Core.Engine.Models;
using DragonBoyAccountManager.Core.Execution;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;
using DragonBoyAccountManager.GUI.Domain.Messaging;
using DragonBoyAccountManager.GUI.Services.Settings;
using DragonBoyAccountManager.GUI.Services.Windowing;

namespace DragonBoyAccountManager.GUI.Services.Engine;

public sealed class CoreGameEngineAdapter : IGameEngineService, IDisposable
{
    private readonly IGameEngine _gameEngine;
    private readonly IWindowManagerService _windowManager;
    private readonly ISettingsService? _settingsService;
    private readonly ConcurrentDictionary<Guid, Account> _accounts = new();
    private bool _isMasked;

    public event Action<SessionEvent>? OnSessionEvent;

    public CoreGameEngineAdapter(
        IGameEngine? gameEngine = null,
        ISettingsService? settingsService = null,
        IWindowManagerService? windowManager = null)
    {
        _gameEngine = gameEngine ?? new GameEngine();
        _settingsService = settingsService;
        _windowManager = windowManager ?? new WindowManagerService();
        _isMasked = settingsService?.Current.IsMasked ?? false;

        _gameEngine.OnSessionEvent += HandleSessionEvent;
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

    private void HandleSessionEvent(SessionEvent evt)
    {
        OnSessionEvent?.Invoke(evt);

        switch (evt)
        {
            case SessionProcessStartedEvent p:
                HandleSessionStarted(p.AccountId, p.Process);
                break;

            case SessionStateChangedEvent s:
                HandleSessionUpdated(s.AccountId, s.Status, s.Snapshot);
                break;

            case SessionProcessExitedEvent e when e.UserInitiated:
                _accounts.TryRemove(e.AccountId, out _);
                break;
        }
    }

    private void HandleSessionUpdated(Guid accountId, GameSessionStatus status, GameSessionSnapshot? snapshot)
    {
        WeakReferenceMessenger.Default.Send(new SessionSnapshotUpdatedMessage(accountId, status, snapshot));

        if (status == GameSessionStatus.Offline)
        {
            _accounts.TryRemove(accountId, out _);
            return;
        }

        if (status == GameSessionStatus.Active && snapshot?.Player != null && !string.IsNullOrWhiteSpace(snapshot.Player.Name))
        {
            var process = _gameEngine.GetProcess(accountId);
            if (process != null)
            {
                var account = _accounts.GetValueOrDefault(accountId);
                string username = account?.Username ?? string.Empty;
                string serverName = account?.ServerName ?? string.Empty;
                string title = FormatWindowTitle(username, serverName, snapshot.Player.Name);
                _windowManager.UpdateWindowTitle(process, title);
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

        _ = Task.Run(async () =>
        {
            await _windowManager.ApplyWindowStyleAsync(process, () =>
            {
                var snap = _gameEngine.GetPlayerSnapshot(accountId);
                string? charName = (snap != null && snap.IsInGame && !string.IsNullOrWhiteSpace(snap.Name)) ? snap.Name : null;
                return FormatWindowTitle(username, serverName, charName);
            }, customW, customH);

            if (_settingsService?.Current.AutoArrangeWindows == true)
            {
                ArrangeWindows();
            }
        });
    }

    public async Task<bool> StartAsync(Account account, string gamePath)
    {
        _accounts[account.Id] = account;

        if (_settingsService?.Current.GameConfig != null)
        {
            GameRmsStorage.SaveGameConfig(_settingsService.Current.GameConfig);
        }

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
            AutoLogin = account.AutoLogin,
            AutoReconnect = _settingsService?.Current.AutoReconnect ?? true,
            ReconnectDelaySeconds = _settingsService?.Current.ReconnectDelaySeconds ?? 3,
            ReconnectPolicyResolver = () => (
                _settingsService?.Current.AutoReconnect ?? true,
                _settingsService?.Current.ReconnectDelaySeconds ?? 3),
            AutoAssignSkills = _settingsService?.Current.AutoAssignSkills == true,
            IsMasked = _isMasked,
            ConfigureSession = session =>
            {
                if (_settingsService?.Current.GameConfig != null)
                {
                    session.ConfigController.ApplyConfig(_settingsService.Current.GameConfig);
                }
            }
        };

        return await _gameEngine.StartAccountAsync(options);
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

    public IReadOnlyCollection<Guid> ActiveSessionIds => _gameEngine.Sessions.ActiveSessionIds;

    public bool FocusWindow(Guid accountId)
    {
        var process = _gameEngine.GetProcess(accountId);
        return process != null && _windowManager.BringToFront(process);
    }

    public void ArrangeWindows(IEnumerable<Guid>? accountIds = null)
    {
        var targetIds = accountIds?.ToList() ?? _gameEngine.Sessions.ActiveSessionIds.ToList();
        var processes = new List<Process>();

        foreach (var id in targetIds)
        {
            var process = _gameEngine.GetProcess(id);
            if (process != null)
            {
                processes.Add(process);
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
        var process = _gameEngine.GetProcess(accountId);
        return process != null && _windowManager.ResizeWindow(process, width, height);
    }

    public void SetMasked(bool isMasked)
    {
        _isMasked = isMasked;
        _gameEngine.SetAllMasked(isMasked);

        foreach (var accountId in _gameEngine.Sessions.ActiveSessionIds)
        {
            var process = _gameEngine.GetProcess(accountId);
            if (process != null)
            {
                var account = _accounts.GetValueOrDefault(accountId);
                string username = account?.Username ?? string.Empty;
                string serverName = account?.ServerName ?? string.Empty;
                var snap = _gameEngine.GetPlayerSnapshot(accountId);
                string? charName = (snap != null && snap.IsInGame && !string.IsNullOrWhiteSpace(snap.Name)) ? snap.Name : null;
                string title = FormatWindowTitle(username, serverName, charName);
                _windowManager.UpdateWindowTitle(process, title);
            }
        }
    }

    public void SendGameKeyDown(Guid accountId, GameKey key) => _gameEngine.SendGameKeyDown(accountId, key);
    public void SendGameKeyUp(Guid accountId, GameKey key) => _gameEngine.SendGameKeyUp(accountId, key);
    public Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120) => _gameEngine.SendGameKeyPressAsync(accountId, key, holdMs);

    public void SendGameKeyDown(Guid accountId, int keyCode) => _gameEngine.SendGameKeyDown(accountId, keyCode);
    public void SendGameKeyUp(Guid accountId, int keyCode) => _gameEngine.SendGameKeyUp(accountId, keyCode);
    public Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120) => _gameEngine.SendGameKeyPressAsync(accountId, keyCode, holdMs);

    public Task<bool> LoginAsync(Guid accountId) => _gameEngine.LoginAsync(accountId);

    public Task<Result> ExecuteCommandAsync(Guid accountId, IGameCommand command, CancellationToken cancellationToken = default) =>
        _gameEngine.ExecuteCommandAsync(accountId, command, cancellationToken);

    public TSnapshot? GetSnapshot<TSnapshot>(Guid accountId) where TSnapshot : class =>
        _gameEngine.GetSnapshot<TSnapshot>(accountId);

    public bool StartBehavior<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class =>
        _gameEngine.StartBehavior<TBehavior, TOptions>(accountId, options);

    public bool UpdateBehaviorOptions<TBehavior, TOptions>(Guid accountId, TOptions options)
        where TBehavior : class, IBehavior<TOptions>
        where TOptions : class =>
        _gameEngine.UpdateBehaviorOptions<TBehavior, TOptions>(accountId, options);

    public bool StopBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior =>
        _gameEngine.StopBehavior<TBehavior>(accountId);

    public bool IsBehaviorRunning<TBehavior>(Guid accountId) where TBehavior : class, IBehavior =>
        _gameEngine.IsBehaviorRunning<TBehavior>(accountId);

    public TBehavior? GetBehavior<TBehavior>(Guid accountId) where TBehavior : class, IBehavior =>
        _gameEngine.GetBehavior<TBehavior>(accountId);

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
