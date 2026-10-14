using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

using DragonBoyAccountManager.Core.Game;
using DragonBoyAccountManager.Core.Game.Objects;
using DragonBoyAccountManager.GUI.Helpers;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.Services.GameEngine;

public sealed class CoreGameEngineAdapter(ISettingsService? settingsService = null) : IGameEngineService, IDisposable
{
    private sealed class ActiveSession(GameInstance instance, GameController controller, Account account) : IDisposable
    {
        public GameInstance Instance { get; } = instance;
        public GameController Controller { get; } = controller;
        public Account Account { get; } = account;
        public CancellationTokenSource Cts { get; } = new();

        private int _isLoggingIn;
        public bool IsLoggingIn => Volatile.Read(ref _isLoggingIn) == 1;
        public bool TryStartLogin() => Interlocked.CompareExchange(ref _isLoggingIn, 1, 0) == 0;
        public void EndLogin() => Interlocked.Exchange(ref _isLoggingIn, 0);

        public void Dispose()
        {
            try { Cts.Cancel(); } catch { }
            Cts.Dispose();
            Controller.Stop();
            Controller.Dispose();
            Instance.Dispose();
        }
    }

    private readonly ConcurrentDictionary<Guid, ActiveSession> _sessions = new();
    private readonly Core.Game.GameLauncher _coreLauncher = new();
    private readonly ISettingsService? _settingsService = settingsService;
    private readonly HashSet<Guid> _userStoppedAccounts = [];
    private readonly object _userStoppedLock = new();

    public event Action<Guid, GameSessionStatus, CharacterSnapshot?>? OnSessionUpdated;
    public event Action<Guid, string>? OnSessionMessageUpdated;

    public async Task<bool> StartAsync(Account account, string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath) || !File.Exists(gamePath))
        {
            return false;
        }

        await StopAsync(account.Id);

        lock (_userStoppedLock)
        {
            _userStoppedAccounts.Remove(account.Id);
        }

        try
        {
            OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.LoggingIn, null);

            var options = new LaunchOptions
            {
                GamePath = gamePath,
                LogPrefix = string.IsNullOrWhiteSpace(account.Username) ? "DBAM" : account.Username
            };

            var instance = await _coreLauncher.LaunchAsync(options);
            var controller = new GameController(instance);
            var session = new ActiveSession(instance, controller, account);

            _sessions[account.Id] = session;

            string serverInfo = !string.IsNullOrWhiteSpace(account.ServerName) ? $" ({account.ServerName})" : "";
            string initialTitle = $"DBAM - {account.Username}{serverInfo}";

            // Tự động đặt tiêu đề chuẩn ngay khi vừa bật game
            _ = Task.Run(() => GameWindowHelper.ApplyWindowStyleAsync(instance.Process, () =>
            {
                if (session.Instance.IsRunning && session.Instance.MyChar.IsInGame && !string.IsNullOrWhiteSpace(session.Instance.MyChar.Name))
                {
                    return $"DBAM - {account.Username}{serverInfo} [{session.Instance.MyChar.Name}]";
                }
                return initialTitle;
            }));

            instance.Exited += (_, _) =>
            {
                if (_sessions.TryRemove(account.Id, out var s))
                {
                    s.Dispose();
                }
                OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.Offline, null);

                bool isUserStopped;
                lock (_userStoppedLock)
                {
                    isUserStopped = _userStoppedAccounts.Contains(account.Id);
                }

                bool autoReconnect = _settingsService?.Current.AutoReconnect ?? true;
                if (!isUserStopped && autoReconnect && account.AutoLogin)
                {
                    int delay = Math.Max(1, _settingsService?.Current.ReconnectDelaySeconds ?? 3);
                    OnSessionMessageUpdated?.Invoke(account.Id, $"Mất kết nối / văng game. Tự động kết nối lại sau {delay}s...");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(delay * 1000);
                            lock (_userStoppedLock)
                            {
                                if (_userStoppedAccounts.Contains(account.Id)) return;
                            }
                            await StartAsync(account, gamePath);
                        }
                        catch { }
                    });
                }
            };

            controller.Ticked += inst =>
            {
                try
                {
                    inst.Reflector.WriteStatic("Main", "isQuitApp", false);
                }
                catch { }

                if (inst.MyChar.IsInGame)
                {
                    var mapSnapshot = inst.Map.GetMapSnapshot();
                    var snapshot = new CharacterSnapshot(
                        inst.MyChar.Name,
                        inst.MyChar.Power,
                        inst.MyChar.Hp,
                        inst.MyChar.Gender,
                        mapSnapshot.MapId >= 0 ? mapSnapshot.MapId : inst.Map.MapId,
                        mapSnapshot.ZoneId >= 0 ? mapSnapshot.ZoneId : inst.Map.ZoneId,
                        !string.IsNullOrWhiteSpace(mapSnapshot.MapName) ? mapSnapshot.MapName : inst.Map.MapName,
                        inst.MyChar.X,
                        inst.MyChar.Y,
                        mapSnapshot.CurrentPlayers,
                        mapSnapshot.MaxPlayers
                    );

                    OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.Active, snapshot);

                    string title = $"DBAM - {account.Username} ({account.ServerName}) [{inst.MyChar.Name}]";
                    GameWindowHelper.UpdateWindowTitle(inst.Process, title);
                }
                else
                {
                    if (account.AutoLogin && !session.IsLoggingIn && inst.IsRunning && !session.Cts.IsCancellationRequested)
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ExecuteLoginAsync(session);
                            }
                            catch { }
                        });
                    }
                }
            };

            controller.Start(TimeSpan.FromSeconds(1));

            if (account.GameSpeed != 1.0f)
            {
                instance.SetTimeScale(account.GameSpeed);
            }

            int customW = _settingsService?.Current.WindowCustomWidth ?? 0;
            int customH = _settingsService?.Current.WindowCustomHeight ?? 0;
            if (customW > 0 && customH > 0)
            {
                instance.SetResolution(customW, customH, false);
            }

            if (_settingsService?.Current.AutoArrangeWindows == true)
            {
                ArrangeWindows(_sessions.Keys);
            }

            // Thêm độ trễ ngắn để game window và các static pointer của instance game được nạp đầy đủ
            await Task.Delay(500);

            // Execute auto login if account has credentials
            if (account.AutoLogin && !string.IsNullOrWhiteSpace(account.Username) && !string.IsNullOrWhiteSpace(account.Password))
            {
                return await ExecuteLoginAsync(session);
            }
            else
            {
                OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.Running, null);
                return true;
            }
        }
        catch (Exception ex)
        {
            OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.Offline, null);
            OnSessionMessageUpdated?.Invoke(account.Id, $"Lỗi khởi động: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> LoginAsync(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            if (string.IsNullOrWhiteSpace(session.Account.Username) || string.IsNullOrWhiteSpace(session.Account.Password))
            {
                OnSessionMessageUpdated?.Invoke(accountId, "Tài khoản hoặc mật khẩu trống.");
                return false;
            }

            session.Account.AutoLogin = true;
            return await ExecuteLoginAsync(session);
        }

        return false;
    }

    private async Task<bool> ExecuteLoginAsync(ActiveSession session)
    {
        if (!session.TryStartLogin())
        {
            return false;
        }

        var account = session.Account;
        var controller = session.Controller;
        var instance = session.Instance;

        OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.LoggingIn, null);
        OnSessionMessageUpdated?.Invoke(account.Id, "Bắt đầu đăng nhập...");

        var progress = new Progress<string>(msg =>
        {
            OnSessionMessageUpdated?.Invoke(account.Id, msg);
        });

        try
        {
            while (instance.IsRunning && !session.Cts.IsCancellationRequested && !instance.MyChar.IsInGame)
            {
                var loginAction = new AutoLoginAction(account.Username, account.Password, account.ServerId);
                bool success = await controller.ExecuteAsync(loginAction, progress, session.Cts.Token);
                if (success || instance.MyChar.IsInGame)
                {
                    OnSessionMessageUpdated?.Invoke(account.Id, "Đăng nhập thành công!");
                    if (instance.MyChar.IsInGame)
                    {
                        var mapSnapshot = instance.Map.GetMapSnapshot();
                        var snapshot = new CharacterSnapshot(
                            instance.MyChar.Name,
                            instance.MyChar.Power,
                            instance.MyChar.Hp,
                            instance.MyChar.Gender,
                            mapSnapshot.MapId >= 0 ? mapSnapshot.MapId : instance.Map.MapId,
                            mapSnapshot.ZoneId >= 0 ? mapSnapshot.ZoneId : instance.Map.ZoneId,
                            !string.IsNullOrWhiteSpace(mapSnapshot.MapName) ? mapSnapshot.MapName : instance.Map.MapName,
                            instance.MyChar.X,
                            instance.MyChar.Y,
                            mapSnapshot.CurrentPlayers,
                            mapSnapshot.MaxPlayers
                        );
                        OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.Active, snapshot);
                    }
                    return true;
                }

                if (!instance.IsRunning || session.Cts.IsCancellationRequested)
                    break;

                if (!account.AutoLogin)
                    break;

                await Task.Delay(1500, session.Cts.Token);
            }

            if (instance.MyChar.IsInGame)
            {
                return true;
            }

            if (instance.IsRunning && !session.Cts.IsCancellationRequested)
            {
                OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.Running, null);
                OnSessionMessageUpdated?.Invoke(account.Id, "Đăng nhập chưa hoàn tất.");
            }
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            if (instance.IsRunning && !session.Cts.IsCancellationRequested)
            {
                OnSessionUpdated?.Invoke(account.Id, GameSessionStatus.Running, null);
                OnSessionMessageUpdated?.Invoke(account.Id, $"Lỗi: {ex.Message}");
            }
            return false;
        }
        finally
        {
            session.EndLogin();
        }
    }

    public Task StopAsync(Guid accountId)
    {
        lock (_userStoppedLock)
        {
            _userStoppedAccounts.Add(accountId);
        }

        if (_sessions.TryRemove(accountId, out var session))
        {
            session.Dispose();
        }

        OnSessionUpdated?.Invoke(accountId, GameSessionStatus.Offline, null);
        return Task.CompletedTask;
    }

    public void StopAll()
    {
        var list = _sessions.ToList();
        _sessions.Clear();

        lock (_userStoppedLock)
        {
            foreach (var (id, _) in list)
            {
                _userStoppedAccounts.Add(id);
            }
        }

        foreach (var (id, session) in list)
        {
            try
            {
                session.Dispose();
            }
            catch { }
            OnSessionUpdated?.Invoke(id, GameSessionStatus.Offline, null);
        }
    }

    public Task StopAllAsync()
    {
        StopAll();
        return Task.CompletedTask;
    }

    public bool IsRunning(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            return session.Instance.IsRunning;
        }
        return false;
    }

    public int? GetProcessId(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            return session.Instance.ProcessId;
        }
        return null;
    }

    public bool FocusWindow(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            return GameWindowHelper.BringToFront(session.Instance.Process);
        }
        return false;
    }

    public void ArrangeWindows(IEnumerable<Guid>? accountIds = null)
    {
        var targetIds = accountIds?.ToList() ?? _sessions.Keys.ToList();
        var processes = new List<Process>();

        int customW = _settingsService?.Current.WindowCustomWidth ?? 0;
        int customH = _settingsService?.Current.WindowCustomHeight ?? 0;

        foreach (var id in targetIds)
        {
            if (_sessions.TryGetValue(id, out var session) && session.Instance.IsRunning)
            {
                if (customW > 0 && customH > 0)
                {
                    session.Instance.SetResolution(customW, customH, false);
                }
                processes.Add(session.Instance.Process);
            }
        }

        if (processes.Count == 0) return;

        var mode = _settingsService?.Current.WindowArrangeMode?.ToLowerInvariant() switch
        {
            "tile" => WindowArrangeMode.Tile,
            "cascade" => WindowArrangeMode.Cascade,
            _ => WindowArrangeMode.Grid
        };

        var options = new WindowArrangeOptions
        {
            Mode = mode,
            Columns = _settingsService?.Current.WindowArrangeColumns ?? 0,
            CustomWidth = _settingsService?.Current.WindowCustomWidth ?? 0,
            CustomHeight = _settingsService?.Current.WindowCustomHeight ?? 0
        };

        GameWindowHelper.ArrangeWindows(processes, options);
    }

    public void SetGameSpeed(Guid accountId, float speed)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            session.Account.GameSpeed = speed;
            session.Instance.SetTimeScale(speed);
        }
    }

    public float GetGameSpeed(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            return session.Instance.GetTimeScale();
        }
        return 1.0f;
    }

    public static int MapGameKeyCode(GameKey key) => key switch
    {
        GameKey.Up => -1,
        GameKey.Down => -2,
        GameKey.Left => -3,
        GameKey.Right => -4,
        GameKey.Ok => -5,
        _ => 0
    };

    public void SendGameKeyDown(Guid accountId, GameKey key)
    {
        int gameCode = MapGameKeyCode(key);
        if (gameCode != 0)
        {
            SendGameKeyDown(accountId, gameCode);
        }
    }

    public void SendGameKeyUp(Guid accountId, GameKey key)
    {
        int gameCode = MapGameKeyCode(key);
        if (gameCode != 0)
        {
            SendGameKeyUp(accountId, gameCode);
        }
    }

    public async Task SendGameKeyPressAsync(Guid accountId, GameKey key, int holdMs = 120)
    {
        int gameCode = MapGameKeyCode(key);
        if (gameCode != 0)
        {
            await SendGameKeyPressAsync(accountId, gameCode, holdMs);
        }
    }

    public void SendGameKeyDown(Guid accountId, int keyCode)
    {
        if (_sessions.TryGetValue(accountId, out var session) && keyCode != 0)
        {
            session.Instance.Canvas.KeyPressedz(keyCode);
        }
    }

    public void SendGameKeyUp(Guid accountId, int keyCode)
    {
        if (_sessions.TryGetValue(accountId, out var session) && keyCode != 0)
        {
            session.Instance.Canvas.KeyReleasedz(keyCode);
        }
    }

    public async Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120)
    {
        if (_sessions.TryGetValue(accountId, out var session) && keyCode != 0)
        {
            session.Instance.Canvas.KeyPressedz(keyCode);
            await Task.Delay(holdMs);
            session.Instance.Canvas.KeyReleasedz(keyCode);
        }
    }

    public bool ChangeZone(Guid accountId, int zoneId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning)
        {
            return session.Instance.Map.ChangeZone(zoneId);
        }
        return false;
    }

    public void RequestZoneList(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.MyChar.IsInGame)
        {
            session.Instance.Map.RequestZoneList();
        }
    }

    public bool MoveToWaypoint(Guid accountId, int targetX, int targetY)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.MyChar.IsInGame)
        {
            return session.Instance.Map.MoveToWaypoint(targetX, targetY);
        }
        return false;
    }

    public bool FocusOrClickObject(Guid accountId, string entityType, int entityId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.MyChar.IsInGame)
        {
            return session.Instance.Map.FocusOrClickObject(entityType, entityId);
        }
        return false;
    }

    public DragonBoyAccountManager.Core.Game.Objects.MapSnapshot? GetMapSnapshot(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.MyChar.IsInGame)
        {
            return session.Instance.Map.GetMapSnapshot();
        }
        return null;
    }

    public bool SendChat(Guid accountId, string text, ChatType type = ChatType.Map)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.MyChar.IsInGame)
        {
            return session.Instance.SendChat(text, type);
        }
        return false;
    }

    public void Dispose()
    {
        StopAll();
    }
}
