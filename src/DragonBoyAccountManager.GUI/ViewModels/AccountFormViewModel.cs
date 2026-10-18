using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DragonBoyAccountManager.Core.Game.Entities;
using DragonBoyAccountManager.Core.Game.Models;
using DragonBoyAccountManager.Core.Game.Snapshots;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.Services.Dialog;
using DragonBoyAccountManager.GUI.Services.GameEngine;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AccountFormViewModel : ObservableObject
{
    private readonly IGameEngineService _gameEngine;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayUsername))]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _selectedServer = "Vũ trụ 1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private bool _autoLogin = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayUsername))]
    [NotifyPropertyChangedFor(nameof(IsHideAccounts))]
    private bool _isMasked;

    public bool IsHideAccounts
    {
        get => IsMasked;
        set => IsMasked = value;
    }

    [ObservableProperty]
    private bool _isApplySpeedToAll = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameSpeedText))]
    private float _gameSpeed = 1.0f;

    public string GameSpeedText => $"{GameSpeed:0.0}x";

    public ObservableCollection<string> AvailableServers { get; } = [];
    public ObservableCollection<WorldZoneInfo> AvailableZones { get; } = [];
    public ObservableCollection<SkillButtonItem> Skills { get; } = [];
    public ObservableCollection<SkillButtonItem> TopSkills { get; } = [];
    public ObservableCollection<SkillButtonItem> BottomSkills { get; } = [];

    private WorldZoneInfo? _selectedZone;
    public WorldZoneInfo? SelectedZone
    {
        get => _selectedZone;
        set
        {
            if (SetProperty(ref _selectedZone, value))
            {
                if (value != null && !_isRefreshingZones)
                {
                    ExecuteChangeZone(value.ZoneId);
                }
            }
        }
    }

    [ObservableProperty]
    private int? _currentZoneId;

    [ObservableProperty]
    private string _zonePlayerDisplay = "0/15";

    private bool _isRefreshingZones;

    private AccountItemViewModel? _editingAccount;
    private Func<IEnumerable<Guid>>? _targetResolver;
    private Func<IEnumerable<Guid>>? _keyTargetResolver;
    private Func<IEnumerable<Guid>>? _speedTargetResolver;

    public string DisplayUsername => IsMasked ? Helpers.TextMaskHelper.MaskText(Username) : Username;

    public bool CanDelete => IsEditing && _editingAccount != null && !_editingAccount.IsRunning;

    public event Func<Account, Task>? OnAccountSaved;
    public event Func<Guid, Task>? OnAccountDeleted;

    private readonly DispatcherTimer _skillRefreshTimer;

    public AccountFormViewModel(IGameEngineService gameEngine, IDialogService dialogService)
    {
        _gameEngine = gameEngine;
        _dialogService = dialogService;

        foreach (var server in ServerOption.DefaultServers)
        {
            AvailableServers.Add(server.ServerName);
        }

        for (int i = 0; i < 10; i++)
        {
            var item = new SkillButtonItem(i, i == 9 ? "Skill 0" : $"Skill {i + 1}");
            Skills.Add(item);
            if (i >= 5)
            {
                TopSkills.Add(item);
            }
            else
            {
                BottomSkills.Add(item);
            }
        }

        _skillRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _skillRefreshTimer.Tick += (_, _) =>
        {
            if (_editingAccount != null && _editingAccount.IsRunning)
            {
                RefreshSkills();
            }
        };
        _skillRefreshTimer.Start();
    }

    public void SetTargetResolver(Func<IEnumerable<Guid>> resolver)
    {
        _targetResolver = resolver;
        _keyTargetResolver = resolver;
        _speedTargetResolver = resolver;
    }

    public void SetKeyTargetResolver(Func<IEnumerable<Guid>> resolver) => _keyTargetResolver = resolver;
    public void SetSpeedTargetResolver(Func<IEnumerable<Guid>> resolver) => _speedTargetResolver = resolver;

    public void SetEditingAccount(AccountItemViewModel? item)
    {
        if (_editingAccount != null)
        {
            _editingAccount.PropertyChanged -= OnEditingAccountPropertyChanged;
        }

        _editingAccount = item;
        if (item != null)
        {
            item.PropertyChanged += OnEditingAccountPropertyChanged;
            Username = item.Username;
            Password = item.Password;
            SelectedServer = item.Server;
            GameSpeed = item.GameSpeed;
            AutoLogin = item.Model.AutoLogin;
            CurrentZoneId = item.ZoneId;
            ZonePlayerDisplay = $"{item.CurrentPlayers ?? 0}/{item.MaxPlayers ?? 15}";
            IsEditing = true;

            if (item.IsRunning)
            {
                RequestZoneList();
                RefreshSkills();
            }
        }
        else
        {
            ClearForm();
        }
        OnPropertyChanged(nameof(CanDelete));
    }

    private void OnEditingAccountPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AccountItemViewModel.IsRunning))
        {
            OnPropertyChanged(nameof(CanDelete));
            if (_editingAccount != null && _editingAccount.IsRunning)
            {
                RequestZoneList();
                RefreshSkills();
            }
        }
        else if (e.PropertyName == nameof(AccountItemViewModel.ZoneId))
        {
            CurrentZoneId = _editingAccount?.ZoneId;
            if (_editingAccount != null)
            {
                ZonePlayerDisplay = $"{_editingAccount.CurrentPlayers ?? 0}/{_editingAccount.MaxPlayers ?? 15}";
                if (SelectedZone?.ZoneId != _editingAccount.ZoneId)
                {
                    _selectedZone = AvailableZones.FirstOrDefault(z => z.ZoneId == _editingAccount.ZoneId);
                    OnPropertyChanged(nameof(SelectedZone));
                }
            }
        }
        else if (e.PropertyName == nameof(AccountItemViewModel.CurrentPlayers) ||
                 e.PropertyName == nameof(AccountItemViewModel.MaxPlayers))
        {
            if (_editingAccount != null)
            {
                ZonePlayerDisplay = $"{_editingAccount.CurrentPlayers ?? 0}/{_editingAccount.MaxPlayers ?? 15}";
            }
        }
    }

    public void ClearForm()
    {
        if (_editingAccount != null)
        {
            _editingAccount.PropertyChanged -= OnEditingAccountPropertyChanged;
        }
        _editingAccount = null;
        Username = string.Empty;
        Password = string.Empty;
        SelectedServer = AvailableServers.FirstOrDefault() ?? "Vũ trụ 1";
        GameSpeed = 1.0f;
        AutoLogin = true;
        CurrentZoneId = null;
        ZonePlayerDisplay = "0/15";
        _selectedZone = null;
        OnPropertyChanged(nameof(SelectedZone));
        AvailableZones.Clear();
        for (int i = 0; i < Skills.Count; i++)
        {
            Skills[i].Name = i == 9 ? "Skill 0" : $"Skill {i + 1}";
            Skills[i].IsSelected = false;
        }
        IsEditing = false;
        OnPropertyChanged(nameof(CanDelete));
    }

    partial void OnGameSpeedChanged(float value)
    {
        if (_editingAccount != null)
        {
            _editingAccount.GameSpeed = value;
            _editingAccount.Model.GameSpeed = value;
        }

        var targets = ResolveSpeedTargets();
        foreach (var id in targets)
        {
            _gameEngine.SetGameSpeed(id, value);
        }
    }

    [RelayCommand]
    private void SetGameSpeed(string speedStr)
    {
        if (float.TryParse(speedStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed))
        {
            GameSpeed = speed;
        }
    }

    [RelayCommand]
    public async Task MoveUpAsync() => await SendKeyPulseAsync(GameKey.Up);

    [RelayCommand]
    public async Task MoveDownAsync() => await SendKeyPulseAsync(GameKey.Down);

    [RelayCommand]
    public async Task MoveLeftAsync() => await SendKeyPulseAsync(GameKey.Left);

    [RelayCommand]
    public async Task MoveRightAsync() => await SendKeyPulseAsync(GameKey.Right);

    [RelayCommand]
    public async Task ActionOkAsync() => await SendKeyPulseAsync(GameKey.Ok);

    [RelayCommand]
    public void UseCapsule()
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.UseCapsule(id);
        }
    }

    [RelayCommand]
    public void UseTdlt()
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.UseTdlt(id);
        }
    }

    [RelayCommand]
    public void UseSkill(object? parameter)
    {
        int slotIndex = 0;
        if (parameter is int idx)
        {
            slotIndex = idx;
        }
        else if (parameter is string str && int.TryParse(str, out int parsed))
        {
            slotIndex = parsed;
        }
        else if (parameter is SkillButtonItem item)
        {
            slotIndex = item.SlotIndex;
        }

        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.UseSkill(id, slotIndex);
        }
    }

    [RelayCommand]
    public void AutoAssignSkills()
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.AutoAssignSkills(id);
        }
        RefreshSkills();
    }

    public void RefreshSkills()
    {
        var accountId = _editingAccount?.Id;
        if (accountId == null && _keyTargetResolver != null)
        {
            accountId = _keyTargetResolver().FirstOrDefault();
        }
        if (accountId == null) return;

        var skills = _gameEngine.GetSkills(accountId.Value);
        var skillMap = skills.ToDictionary(s => s.Slot, s => s);
        for (int i = 0; i < Skills.Count; i++)
        {
            if (skillMap.TryGetValue(i, out var sInfo))
            {
                string expectedName = !string.IsNullOrWhiteSpace(sInfo.Name)
                    ? sInfo.Name
                    : (i == 9 ? "Skill 0" : $"Skill {i + 1}");

                if (Skills[i].Name != expectedName)
                {
                    Skills[i].Name = expectedName;
                }
                if (Skills[i].IsSelected != sInfo.IsSelected)
                {
                    Skills[i].IsSelected = sInfo.IsSelected;
                }
            }
            else
            {
                string defaultName = i == 9 ? "Skill 0" : $"Skill {i + 1}";
                if (Skills[i].Name != defaultName)
                {
                    Skills[i].Name = defaultName;
                }
                if (Skills[i].IsSelected)
                {
                    Skills[i].IsSelected = false;
                }
            }
        }
    }

    [RelayCommand]
    public void NextZone()
    {
        int next = (CurrentZoneId ?? 0) + 1;
        ExecuteChangeZone(next);
    }

    [RelayCommand]
    public void PreviousZone()
    {
        int prev = (CurrentZoneId ?? 0) - 1;
        if (prev < 0) prev = 0;
        ExecuteChangeZone(prev);
    }

    public void ExecuteChangeZone(int targetZone)
    {
        if (targetZone < 0) return;
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.ChangeZone(id, targetZone);
        }
    }

    public void RequestZoneList()
    {
        var accountId = _editingAccount?.Id;
        if (accountId == null && _keyTargetResolver != null)
        {
            accountId = _keyTargetResolver().FirstOrDefault();
        }
        if (accountId == null) return;

        _gameEngine.RequestZoneList(accountId.Value);
        var snapshot = _gameEngine.GetWorldSnapshot(accountId.Value);
        if (snapshot != null && snapshot.Zones.Count > 0)
        {
            UpdateZones(snapshot.Zones, snapshot.ZoneId);
        }

        _ = Task.Run(async () =>
        {
            await Task.Delay(350);
            var freshSnapshot = _gameEngine.GetWorldSnapshot(accountId.Value);
            if (freshSnapshot != null && freshSnapshot.Zones.Count > 0)
            {
                var dispatcher = App.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    await dispatcher.InvokeAsync(() => UpdateZones(freshSnapshot.Zones, freshSnapshot.ZoneId));
                }
            }
        });
    }

    public void UpdateZones(IReadOnlyList<WorldZoneInfo> newZones, int currentZoneId)
    {
        _isRefreshingZones = true;
        try
        {
            bool needRebuild = AvailableZones.Count != newZones.Count;
            if (!needRebuild)
            {
                for (int i = 0; i < newZones.Count; i++)
                {
                    if (AvailableZones[i].ZoneId != newZones[i].ZoneId)
                    {
                        needRebuild = true;
                        break;
                    }
                }
            }

            if (needRebuild)
            {
                AvailableZones.Clear();
                foreach (var z in newZones)
                {
                    AvailableZones.Add(z);
                }
            }
            else
            {
                for (int i = 0; i < newZones.Count; i++)
                {
                    if (AvailableZones[i] != newZones[i])
                    {
                        AvailableZones[i] = newZones[i];
                    }
                }
            }

            CurrentZoneId = currentZoneId;
            if (SelectedZone?.ZoneId != currentZoneId)
            {
                _selectedZone = AvailableZones.FirstOrDefault(z => z.ZoneId == currentZoneId);
                OnPropertyChanged(nameof(SelectedZone));
            }
        }
        finally
        {
            _isRefreshingZones = false;
        }
    }

    public void StartHoldingKey(GameKey key)
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.SendGameKeyDown(id, key);
        }
    }

    public void StopHoldingKey(GameKey key)
    {
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            _gameEngine.SendGameKeyUp(id, key);
        }
    }

    public async Task SendKeyPulseAsync(GameKey key, int holdMs = 120)
    {
        var targets = ResolveKeyTargets().ToList();
        if (targets.Count == 0) return;

        var tasks = targets.Select(id => _gameEngine.SendGameKeyPressAsync(id, key, holdMs));
        await Task.WhenAll(tasks);
    }

    private IEnumerable<Guid> ResolveKeyTargets()
    {
        if (_keyTargetResolver != null)
        {
            return _keyTargetResolver();
        }

        if (_targetResolver != null)
        {
            return _targetResolver();
        }

        if (_editingAccount != null)
        {
            return [_editingAccount.Id];
        }

        return [];
    }

    private IEnumerable<Guid> ResolveSpeedTargets()
    {
        if (_speedTargetResolver != null)
        {
            return _speedTargetResolver();
        }

        if (_targetResolver != null)
        {
            return _targetResolver();
        }

        if (_editingAccount != null)
        {
            return [_editingAccount.Id];
        }

        return [];
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
            return;

        var serverOpt = ServerOption.DefaultServers.FirstOrDefault(s => string.Equals(s.ServerName, SelectedServer, StringComparison.OrdinalIgnoreCase));
        int serverId = serverOpt?.Id ?? 0;

        Account account;
        if (_editingAccount != null)
        {
            account = _editingAccount.Model;
            account.Username = Username.Trim();
            account.Password = Password.Trim();
            account.ServerName = SelectedServer;
            account.ServerId = serverId;
            account.GameSpeed = GameSpeed;
            account.AutoLogin = AutoLogin;
        }
        else
        {
            account = new Account
            {
                Username = Username.Trim(),
                Password = Password.Trim(),
                ServerName = SelectedServer,
                ServerId = serverId,
                GameSpeed = GameSpeed,
                AutoLogin = AutoLogin
            };
        }

        if (OnAccountSaved != null)
        {
            await OnAccountSaved.Invoke(account);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        ClearForm();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_editingAccount == null) return;

        if (_editingAccount.IsRunning)
        {
            _dialogService.ShowWarning("Không thể xóa", $"Tài khoản '{_editingAccount.Username}' đang chạy game. Vui lòng đóng game trước khi xóa!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("Xác nhận xóa", $"Bạn có chắc chắn muốn xóa tài khoản '{_editingAccount.Username}'?");
        if (!confirm) return;

        if (OnAccountDeleted != null)
        {
            await OnAccountDeleted.Invoke(_editingAccount.Id);
        }
        ClearForm();
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordVisible = !IsPasswordVisible;
    }

    [RelayCommand]
    private void ToggleHideAccounts()
    {
        IsMasked = !IsMasked;
    }
}
