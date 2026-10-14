using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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

    private AccountItemViewModel? _editingAccount;
    private Func<IEnumerable<Guid>>? _targetResolver;
    private Func<IEnumerable<Guid>>? _keyTargetResolver;
    private Func<IEnumerable<Guid>>? _speedTargetResolver;

    public string DisplayUsername => IsMasked ? Helpers.TextMaskHelper.MaskText(Username) : Username;

    public bool CanDelete => IsEditing && _editingAccount != null && !_editingAccount.IsRunning;

    public event Func<Account, Task>? OnAccountSaved;
    public event Func<Guid, Task>? OnAccountDeleted;

    public AccountFormViewModel(IGameEngineService gameEngine, IDialogService dialogService)
    {
        _gameEngine = gameEngine;
        _dialogService = dialogService;

        foreach (var server in ServerOption.DefaultServers)
        {
            AvailableServers.Add(server.ServerName);
        }
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
            IsEditing = true;
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
        var targets = ResolveKeyTargets();
        foreach (var id in targets)
        {
            await _gameEngine.SendGameKeyPressAsync(id, key, holdMs);
        }
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
