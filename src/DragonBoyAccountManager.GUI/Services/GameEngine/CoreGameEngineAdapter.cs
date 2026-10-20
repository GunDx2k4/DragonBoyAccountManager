using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;
using DragonBoyAccountManager.GUI.Messaging;

using DragonBoyAccountManager.Core.Automation.Actions.Abstractions;
using DragonBoyAccountManager.Core.Automation.Actions.Primitives;
using DragonBoyAccountManager.Core.Automation.Routines.Chat;
using DragonBoyAccountManager.Core.Automation.Routines.Login;
using DragonBoyAccountManager.Core.Automation.Routines.Training;
using DragonBoyAccountManager.Core.Domain.Contracts;
using DragonBoyAccountManager.Core.Domain.Entities;
using DragonBoyAccountManager.Core.Domain.Enums;
using DragonBoyAccountManager.Core.Domain.Models;
using DragonBoyAccountManager.Core.Domain.Snapshots;
using DragonBoyAccountManager.Core.Lifecycle;
using DragonBoyAccountManager.Core.Lifecycle.Options;
using DragonBoyAccountManager.GUI.Common.Enums;
using DragonBoyAccountManager.GUI.Common.Models;
using DragonBoyAccountManager.GUI.Services.Windowing;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.Services.GameEngine;

public sealed class CoreGameEngineAdapter(ISettingsService? settingsService = null, IWindowManagerService? windowManager = null) : IGameEngineService, IDisposable
{
    private readonly IWindowManagerService _windowManager = windowManager ?? new WindowManagerService();
    private sealed class ActiveSession(GameInstance instance, GameController controller, Account account) : IDisposable
    {
        public GameInstance Instance { get; } = instance;
        public GameController Controller { get; } = controller;
        public Account Account { get; } = account;
        public CancellationTokenSource Cts { get; } = new();
        public bool HasAssignedSkills { get; set; }

        private int _isLoggingIn;
        public bool IsLoggingIn => Volatile.Read(ref _isLoggingIn) == 1;
        public bool TryStartLogin() => Interlocked.CompareExchange(ref _isLoggingIn, 1, 0) == 0;
        public void EndLogin() => Interlocked.Exchange(ref _isLoggingIn, 0);

        public async Task StopAsync()
        {
            try { Cts.Cancel(); } catch { }
            Controller.Routines.StopAll();
            Instance.Kill();
            await Controller.StopAsync();
            Cts.Dispose();
            Controller.Dispose();
            Instance.Dispose();
        }

        public void Dispose()
        {
            try { Cts.Cancel(); } catch { }
            Controller.Routines.StopAll();
            Instance.Kill();
            Controller.Stop();
            Cts.Dispose();
            Controller.Dispose();
            Instance.Dispose();
        }
    }

    private readonly ConcurrentDictionary<Guid, ActiveSession> _sessions = new();
    private readonly GameLauncher _coreLauncher = new();
    private readonly ISettingsService? _settingsService = settingsService;
    private readonly HashSet<Guid> _userStoppedAccounts = [];
    private readonly object _userStoppedLock = new();

    public event Action<Guid, GameSessionStatus, GameSessionSnapshot?>? OnSessionUpdated;
    public event Action<Guid, string>? OnSessionMessageUpdated;
    public event Action<Guid, ActionExecutionReport>? OnActionReported;
    public event Action<Guid, string, int>? OnChatSent;
    public event Action<Guid, string>? OnAutoTrainStatusChanged;
    public event Action<Guid, bool>? OnAutoTrainStateChanged;

    private void NotifySessionUpdated(Guid accountId, GameSessionStatus status, GameSessionSnapshot? snapshot)
    {
        OnSessionUpdated?.Invoke(accountId, status, snapshot);
        WeakReferenceMessenger.Default.Send(new SessionSnapshotUpdatedMessage(accountId, status, snapshot));
    }

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
            NotifySessionUpdated(account.Id, GameSessionStatus.LoggingIn, null);

            int customW = _settingsService?.Current.WindowCustomWidth ?? 0;
            int customH = _settingsService?.Current.WindowCustomHeight ?? 0;

            PreSetServerRms(account.ServerId);

            var options = new LaunchOptions
            {
                SessionId = account.Id,
                GamePath = gamePath,
                LogPrefix = string.IsNullOrWhiteSpace(account.Username) ? "DBAM" : account.Username
            };

            var instance = await _coreLauncher.LaunchAsync(options);
            var controller = new GameController(instance);
            var session = new ActiveSession(instance, controller, account);

            _sessions[account.Id] = session;

            controller.ActionReported += report => OnActionReported?.Invoke(account.Id, report);

            controller.Routines.RoutineStateChanged += (routine, isRunning) =>
            {
                if (routine is AutoTrainRoutine)
                {
                    OnAutoTrainStateChanged?.Invoke(account.Id, isRunning);
                }
            };

            string serverInfo = !string.IsNullOrWhiteSpace(account.ServerName) ? $" ({account.ServerName})" : "";
            string initialTitle = $"DBAM - {account.Username}{serverInfo}";

            _ = Task.Run(() => _windowManager.ApplyWindowStyleAsync(instance.Process, () =>
            {
                if (session.Instance.IsRunning && session.Instance.Player.IsInGame && !string.IsNullOrWhiteSpace(session.Instance.Player.Name))
                {
                    return $"DBAM - {account.Username}{serverInfo} [{session.Instance.Player.Name}]";
                }
                return initialTitle;
            }, customW, customH));

            instance.Exited += (_, _) =>
            {
                if (_sessions.TryRemove(account.Id, out var s))
                {
                    s.Dispose();
                }
                NotifySessionUpdated(account.Id, GameSessionStatus.Offline, null);

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
                if (!inst.IsRunning || session.Cts.IsCancellationRequested)
                    return;

                if (inst.Engine.IsQuitRequested)
                {
                    controller.Stop();
                    inst.Kill();
                    return;
                }

                if (!inst.IsRunning || session.Cts.IsCancellationRequested)
                    return;

                try
                {
                    if (inst.Player.IsInGame)
                    {
                        if (!session.HasAssignedSkills && _settingsService?.Current.AutoAssignSkills == true)
                        {
                            session.HasAssignedSkills = true;
                            _ = Task.Run(() =>
                            {
                                try
                                {
                                    inst.Player.AutoAssignSkills();
                                }
                                catch { }
                            });
                        }

                        PublishSessionUpdate(session);
                    }
                    else
                    {
                        session.HasAssignedSkills = false;
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
                }
                catch { }
            };

            controller.Start(TimeSpan.FromSeconds(1));

            if (account.GameSpeed != 1.0f)
            {
                instance.Engine.SetTimeScale(account.GameSpeed);
            }

            if (_settingsService?.Current.AutoArrangeWindows == true)
            {
                ArrangeWindows(_sessions.Keys);
            }

            await Task.Delay(500);

            if (account.AutoLogin && !string.IsNullOrWhiteSpace(account.Username) && !string.IsNullOrWhiteSpace(account.Password))
            {
                return await ExecuteLoginAsync(session);
            }
            else
            {
                NotifySessionUpdated(account.Id, GameSessionStatus.Running, null);
                return true;
            }
        }
        catch (Exception ex)
        {
            NotifySessionUpdated(account.Id, GameSessionStatus.Offline, null);
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
        var account = session.Account;
        var controller = session.Controller;
        var instance = session.Instance;

        if (!session.TryStartLogin())
        {
            return false;
        }

        NotifySessionUpdated(account.Id, GameSessionStatus.LoggingIn, null);
        OnSessionMessageUpdated?.Invoke(account.Id, "Bắt đầu đăng nhập...");

        var progress = new Progress<string>(msg =>
        {
            OnSessionMessageUpdated?.Invoke(account.Id, msg);
        });

        try
        {
            var loginRoutine = new AutoLoginRoutine(new AutoLoginOptions(
                account.Username,
                account.Password,
                account.ServerId,
                account.ServerName,
                maxAttempts: account.AutoLogin ? 15 : 3));

            await controller.Routines.StartAsync(loginRoutine, progress, session.Cts.Token);

            if (instance.Player.IsInGame)
            {
                OnSessionMessageUpdated?.Invoke(account.Id, "Đăng nhập thành công!");
                if (!session.HasAssignedSkills && _settingsService?.Current.AutoAssignSkills == true)
                {
                    session.HasAssignedSkills = true;
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            instance.Player.AutoAssignSkills();
                        }
                        catch { }
                    });
                }

                var worldSnapshot = instance.World.GetSnapshot();
                var playerSnapshot = instance.Player.GetSnapshot();
                var sessionSnapshot = new GameSessionSnapshot(playerSnapshot, worldSnapshot);
                NotifySessionUpdated(account.Id, GameSessionStatus.Active, sessionSnapshot);
                return true;
            }

            if (instance.IsRunning && !session.Cts.IsCancellationRequested)
            {
                NotifySessionUpdated(account.Id, GameSessionStatus.Running, null);
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
                NotifySessionUpdated(account.Id, GameSessionStatus.Running, null);
                OnSessionMessageUpdated?.Invoke(account.Id, $"Lỗi: {ex.Message}");
            }
            return false;
        }
        finally
        {
            session.EndLogin();
        }
    }

    public async Task StopAsync(Guid accountId)
    {
        lock (_userStoppedLock)
        {
            _userStoppedAccounts.Add(accountId);
        }

        if (_sessions.TryRemove(accountId, out var session))
        {
            await session.StopAsync();
        }

        NotifySessionUpdated(accountId, GameSessionStatus.Offline, null);
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
            NotifySessionUpdated(id, GameSessionStatus.Offline, null);
        }
    }

    public async Task StopAllAsync()
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
                await session.StopAsync();
            }
            catch { }
            NotifySessionUpdated(id, GameSessionStatus.Offline, null);
        }
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
            return _windowManager.BringToFront(session.Instance.Process);
        }
        return false;
    }

    public void ArrangeWindows(IEnumerable<Guid>? accountIds = null)
    {
        var targetIds = accountIds?.ToList() ?? _sessions.Keys.ToList();
        var processes = new List<Process>();

        foreach (var id in targetIds)
        {
            if (_sessions.TryGetValue(id, out var session) && session.Instance.IsRunning)
            {
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
            Columns = _settingsService?.Current.WindowArrangeColumns ?? 0
        };

        _windowManager.ArrangeWindows(processes, options);
    }

    public bool SetResolution(Guid accountId, int width, int height)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            return _windowManager.ResizeWindow(session.Instance.Process, width, height);
        }
        return false;
    }

    public void SetGameSpeed(Guid accountId, float speed)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            session.Account.GameSpeed = speed;
            session.Instance.Engine.SetTimeScale(speed);
        }
    }

    public float GetGameSpeed(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            return session.Instance.Engine.TimeScale;
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
        GameKey.Focus => -26,
        GameKey.PickItem => 113,
        GameKey.UsePean => 32,
        GameKey.ShowPanel => -21,
        GameKey.HidePanel => -22,
        GameKey.Yes => 121,
        GameKey.Chat => 114,
        GameKey.Back => -8,
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
            session.Instance.UI.KeyPressed(keyCode);
        }
    }

    public void SendGameKeyUp(Guid accountId, int keyCode)
    {
        if (_sessions.TryGetValue(accountId, out var session) && keyCode != 0)
        {
            session.Instance.UI.KeyReleased(keyCode);
        }
    }

    public async Task SendGameKeyPressAsync(Guid accountId, int keyCode, int holdMs = 120)
    {
        if (_sessions.TryGetValue(accountId, out var session) && keyCode != 0)
        {
            session.Instance.UI.KeyPressed(keyCode);
            await Task.Delay(holdMs);
            session.Instance.UI.KeyReleased(keyCode);
        }
    }

    public bool ChangeZone(Guid accountId, int zoneId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning)
        {
            return session.Instance.World.ChangeZone(zoneId);
        }
        return false;
    }

    public void RequestZoneList(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            session.Instance.World.RequestZoneList();
        }
    }

    public void CloseZonePanel(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            session.Instance.UI.CloseZonePanel();
        }
    }

    public bool MoveToWaypoint(Guid accountId, int targetX, int targetY)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            bool res = session.Instance.World.MoveToWaypoint(targetX, targetY);
            PublishSessionUpdate(session);
            return res;
        }
        return false;
    }

    public async Task<bool> AutoMoveAsync(Guid accountId, int targetX, int targetY, bool isFastTeleport = false, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            if (session.Instance.Player.IsChangingMap)
            {
                return false;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(session.Cts.Token, cancellationToken);

            var progress = new Progress<string>(_ =>
            {
                PublishSessionUpdate(session);
            });
            var moveAction = new AutoMoveAction(targetX, targetY, isFastTeleport);
            var result = await session.Controller.ExecuteAsync(moveAction, progress, cancellationToken: linkedCts.Token);
            PublishSessionUpdate(session);
            return result.IsSuccess;
        }
        return false;
    }

    public bool FocusOrClickObject(Guid accountId, string entityType, int entityId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            bool res = session.Instance.World.FocusTarget(entityType, entityId);
            PublishSessionUpdate(session);
            return res;
        }
        return false;
    }

    public WorldSnapshot? GetWorldSnapshot(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            try
            {
                return session.Instance.World.GetSnapshot();
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public PlayerSnapshot? GetPlayerSnapshot(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            try
            {
                return session.Instance.Player.GetSnapshot();
            }
            catch
            {
                return null;
            }
        }
        return null;
    }

    public bool SendChat(Guid accountId, string text, ChatType type = ChatType.Map)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            return session.Instance.Network.SendChat(text, type);
        }
        return false;
    }

    public async Task<bool> SendChatAsync(Guid accountId, string text, ChatType type = ChatType.Map, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(session.Cts.Token, cancellationToken);
            var action = new SendChatAction(text, type);
            var result = await session.Controller.ExecuteAsync(action, cancellationToken: linkedCts.Token);
            return result.IsSuccess;
        }
        return false;
    }

    public void StartAutoChat(Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning)
        {
            StopAutoChat(accountId);
            var routine = new AutoChatRoutine(messages, interval, type, isRandom);
            routine.ChatSent += (msg, count) => OnChatSent?.Invoke(accountId, msg, count);
            _ = session.Controller.Routines.StartAsync(routine, null, session.Cts.Token);
        }
    }

    public void UpdateAutoChatConfig(Guid accountId, IReadOnlyList<string> messages, TimeSpan interval, ChatType type = ChatType.Map, bool isRandom = false)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            var routine = session.Controller.Routines.Get<AutoChatRoutine>();
            routine?.UpdateConfig(messages, interval, type, isRandom);
        }
    }

    public void StopAutoChat(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            session.Controller.Routines.Stop<AutoChatRoutine>();
        }
    }

    public bool IsAutoChatRunning(Guid accountId)
    {
        return _sessions.TryGetValue(accountId, out var session) && session.Controller.Routines.IsRunning<AutoChatRoutine>();
    }

    public void StartAutoTrain(Guid accountId, AutoTrainOptions options)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning)
        {
            StopAutoTrain(accountId);

            try
            {
                session.Instance.World.SuppressAutoPlay();
            }
            catch { }

            var routine = new AutoTrainRoutine(options);

            var progress = new Progress<string>(msg =>
            {
                OnAutoTrainStatusChanged?.Invoke(accountId, msg);
                session.Controller.ReportExecution(ActionExecutionReport.Running(routine.Name, msg));
            });

            _ = session.Controller.Routines.StartAsync(routine, progress, session.Cts.Token);
        }
    }

    public void UpdateAutoTrainOptions(Guid accountId, AutoTrainOptions options)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            var routine = session.Controller.Routines.Get<AutoTrainRoutine>();
            routine?.UpdateOptions(options);
        }
    }

    public void StopAutoTrain(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session))
        {
            session.Controller.Routines.Stop<AutoTrainRoutine>();
            try
            {
                session.Instance.World.SuppressAutoPlay();
            }
            catch { }
            OnAutoTrainStateChanged?.Invoke(accountId, false);
            OnAutoTrainStatusChanged?.Invoke(accountId, "Đã dừng tự động luyện tập.");
            session.Controller.ReportExecution(new ActionExecutionReport("Tự động luyện tập", ActionStatus.Success, "Đã dừng tự động luyện tập.", DateTime.UtcNow));
        }
    }

    public bool IsAutoTrainRunning(Guid accountId)
    {
        return _sessions.TryGetValue(accountId, out var session) && session.Controller.Routines.IsRunning<AutoTrainRoutine>();
    }

    public (bool Success, string Message) UseCapsule(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            var res = session.Instance.Player.UseCapsule();
            session.Controller.ReportExecution(new ActionExecutionReport("Sử dụng Capsule", res.Success ? ActionStatus.Success : ActionStatus.Failure, res.Message, DateTime.UtcNow));
            return res;
        }
        return (false, "Tài khoản chưa vào game");
    }

    public (bool Success, string Message) UseTdlt(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            var res = session.Instance.Player.UseTdlt();
            session.Controller.ReportExecution(new ActionExecutionReport("Sử dụng TDLT", res.Success ? ActionStatus.Success : ActionStatus.Failure, res.Message, DateTime.UtcNow));
            return res;
        }
        return (false, "Tài khoản chưa vào game");
    }

    public (bool Success, string Message) UsePean(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            var res = session.Instance.Player.UsePean();
            session.Controller.ReportExecution(new ActionExecutionReport("Sử dụng đậu thần", res.Success ? ActionStatus.Success : ActionStatus.Failure, res.Message, DateTime.UtcNow));
            return res;
        }
        return (false, "Tài khoản chưa vào game");
    }

    public (bool Success, string Message) SelectSkill(Guid accountId, int slotIndex)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            var res = session.Instance.Player.SelectSkill(slotIndex);
            session.Controller.ReportExecution(new ActionExecutionReport("Chọn kỹ năng", res.Success ? ActionStatus.Success : ActionStatus.Failure, res.Message, DateTime.UtcNow));
            return res;
        }
        return (false, "Tài khoản chưa vào game");
    }

    public (bool Success, string Message) UseSkill(Guid accountId, int slotIndex)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            var res = session.Instance.Player.UseSkill(slotIndex);
            session.Controller.ReportExecution(new ActionExecutionReport("Dùng kỹ năng", res.Success ? ActionStatus.Success : ActionStatus.Failure, res.Message, DateTime.UtcNow));
            return res;
        }
        return (false, "Tài khoản chưa vào game");
    }

    public IReadOnlyList<GameSkillInfo> GetSkills(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            return session.Instance.Player.GetSkills();
        }
        return [];
    }

    public (bool Success, string Message) AutoAssignSkills(Guid accountId)
    {
        if (_sessions.TryGetValue(accountId, out var session) && session.Instance.IsRunning && session.Instance.Player.IsInGame)
        {
            var res = session.Instance.Player.AutoAssignSkills();
            session.Controller.ReportExecution(new ActionExecutionReport("Gán phím kỹ năng", res.Success ? ActionStatus.Success : ActionStatus.Failure, res.Message, DateTime.UtcNow));
            return res;
        }
        return (false, "Tài khoản chưa vào game");
    }

    private void PublishSessionUpdate(ActiveSession session)
    {
        var inst = session.Instance;
        if (!inst.IsRunning || session.Cts.IsCancellationRequested) return;

        try
        {
            if (inst.Player.IsInGame)
            {
                var worldSnapshot = inst.World.GetSnapshot();
                var playerSnapshot = inst.Player.GetSnapshot();
                var sessionSnapshot = new GameSessionSnapshot(playerSnapshot, worldSnapshot);

                NotifySessionUpdated(session.Account.Id, GameSessionStatus.Active, sessionSnapshot);

                string title = $"DBAM - {session.Account.Username} ({session.Account.ServerName}) [{playerSnapshot.Name}]";
                _windowManager.UpdateWindowTitle(inst.Process, title);
            }
        }
        catch { }
    }

    private static void PreSetServerRms(int serverId)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string svSelectPath = Path.Combine(appData, "AppData", "LocalLow", "Team", "DragonBoy250", "svselect");
            string? dir = Path.GetDirectoryName(svSelectPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(svSelectPath, [(byte)serverId]);
        }
        catch { }
    }

    public void Dispose()
    {
        StopAll();
    }
}
