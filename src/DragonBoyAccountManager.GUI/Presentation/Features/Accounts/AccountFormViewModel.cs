using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;


namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AccountFormViewModel : ObservableObject
{
    private readonly IGameEngineService _gameEngine;
    private readonly IDialogService _dialogService;
    private readonly IGameImageService _gameImageService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayUsername))]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _selectedServer = "Vu tr? 1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDelete))]
    private bool _isEditing;

    [ObservableProperty]
    private bool _isPasswordVisible;

    [ObservableProperty]
    private bool _autoLogin = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayUsername))]
    private bool _isMasked;

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
    private readonly IActiveSelectionService? _selectionService;
    private Func<IEnumerable<Guid>>? _targetResolver;
    private Func<IEnumerable<Guid>>? _keyTargetResolver;
    private Func<IEnumerable<Guid>>? _speedTargetResolver;

    public string DisplayUsername => IsMasked ? TextMaskHelper.MaskText(Username) : Username;

    public bool CanDelete => IsEditing && _editingAccount != null && !_editingAccount.IsRunning;

    public event Func<Account, Task>? OnAccountSaved;
    public event Func<Guid, Task>? OnAccountDeleted;

    private readonly DispatcherTimer _skillRefreshTimer;
    private readonly DispatcherTimer _cooldownTimer;

    public AccountFormViewModel(
        IGameEngineService gameEngine,
        IDialogService dialogService,
        IGameImageService gameImageService,
        IActiveSelectionService? selectionService = null)
    {
        _gameEngine = gameEngine;
        _dialogService = dialogService;
        _gameImageService = gameImageService;
        _selectionService = selectionService;

        if (_selectionService != null)
        {
            _selectionService.SelectedAccountChanged += SetEditingAccount;
        }

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
        _cooldownTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _cooldownTimer.Tick += (_, _) => UpdateCooldowns();

        WeakReferenceMessenger.Default.Register<AccountSelectedMessage>(this, (_, m) =>
        {
            SetEditingAccount(m.Account);
        });

        WeakReferenceMessenger.Default.Register<SessionSnapshotUpdatedMessage>(this, (_, m) =>
        {
            if (_editingAccount?.Id == m.AccountId && m.Status == GameSessionStatus.Active)
            {
                var dispatcher = App.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    _ = dispatcher.InvokeAsync(RefreshSkills);
                }
            }
        });
    }

    public void StartPolling()
    {
        if (!_skillRefreshTimer.IsEnabled)
        {
            _skillRefreshTimer.Start();
        }
        if (!_cooldownTimer.IsEnabled)
        {
            _cooldownTimer.Start();
        }
    }

    public void StopPolling()
    {
        if (_skillRefreshTimer.IsEnabled)
        {
            _skillRefreshTimer.Stop();
        }
        if (_cooldownTimer.IsEnabled)
        {
            _cooldownTimer.Stop();
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
        SelectedServer = AvailableServers.FirstOrDefault() ?? "Vu tr? 1";
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
            _dialogService.ShowWarning("Kh�ng th? x�a", $"T�i kho?n '{_editingAccount.Username}' dang ch?y game. Vui l�ng d�ng game tru?c khi x�a!");
            return;
        }

        bool confirm = _dialogService.ShowConfirmation("X�c nh?n x�a", $"B?n c� ch?c ch?n mu?n x�a t�i kho?n '{_editingAccount.Username}'?");
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

