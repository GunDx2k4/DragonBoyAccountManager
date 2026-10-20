using CommunityToolkit.Mvvm.ComponentModel;

using DragonBoyAccountManager.GUI.Helpers;
using DragonBoyAccountManager.GUI.Models;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class AccountItemViewModel : ObservableObject
{
    public Account Model { get; }

    public Guid Id => Model.Id;
    public string Username => Model.Username;
    public string Password => Model.Password;
    public string Server => Model.ServerName;
    public int ServerId => Model.ServerId;

    public bool AutoLogin => Model.AutoLogin;
    public AccountAutoSettings AutoSettings => Model.AutoSettings;

    [ObservableProperty]
    private bool _isAutoTrainRunning;

    [ObservableProperty]
    private string _autoTrainStatusText = "Chưa kích hoạt";

    [ObservableProperty]
    private bool _isAutoChatRunning;

    [ObservableProperty]
    private string _autoChatStatusText = "Chưa kích hoạt";

    [ObservableProperty]
    private int _chatSentCount;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayUsername))]
    [NotifyPropertyChangedFor(nameof(DisplayCharacterName))]
    private bool _isMasked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplayName))]
    [NotifyPropertyChangedFor(nameof(StatusTooltip))]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private GameSessionStatus _status = GameSessionStatus.Offline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDisplayName))]
    [NotifyPropertyChangedFor(nameof(StatusTooltip))]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionMessage))]
    [NotifyPropertyChangedFor(nameof(HasActionMessage))]
    [NotifyPropertyChangedFor(nameof(IsActionFailed))]
    [NotifyPropertyChangedFor(nameof(StatusTooltip))]
    private ActionExecutionReport _lastActionReport = ActionExecutionReport.Idle();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    private int? _processId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCharacterName))]
    [NotifyPropertyChangedFor(nameof(DisplayCharacterName))]
    private string? _characterName;

    [ObservableProperty]
    private long? _characterPower;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayHp))]
    [NotifyPropertyChangedFor(nameof(HasHpMp))]
    private long? _characterHP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayHp))]
    [NotifyPropertyChangedFor(nameof(HasHpMp))]
    private long? _characterMaxHP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayMp))]
    [NotifyPropertyChangedFor(nameof(HasHpMp))]
    private long? _characterMP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayMp))]
    [NotifyPropertyChangedFor(nameof(HasHpMp))]
    private long? _characterMaxMP;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenderDisplayName))]
    private int? _characterGender;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMapInfo))]
    [NotifyPropertyChangedFor(nameof(DisplayMapName))]
    [NotifyPropertyChangedFor(nameof(DisplayZone))]
    [NotifyPropertyChangedFor(nameof(DisplayMapFullInfo))]
    private int? _mapId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMapInfo))]
    [NotifyPropertyChangedFor(nameof(DisplayMapName))]
    [NotifyPropertyChangedFor(nameof(DisplayZone))]
    [NotifyPropertyChangedFor(nameof(DisplayMapFullInfo))]
    private int? _zoneId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMapInfo))]
    [NotifyPropertyChangedFor(nameof(DisplayMapName))]
    [NotifyPropertyChangedFor(nameof(DisplayZone))]
    [NotifyPropertyChangedFor(nameof(DisplayZoneAndCoords))]
    [NotifyPropertyChangedFor(nameof(DisplayMapFullInfo))]
    private string? _mapName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayCoordinates))]
    [NotifyPropertyChangedFor(nameof(DisplayZoneAndCoords))]
    [NotifyPropertyChangedFor(nameof(DisplayMapFullInfo))]
    [NotifyPropertyChangedFor(nameof(HasCoordinates))]
    private int? _charX;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayCoordinates))]
    [NotifyPropertyChangedFor(nameof(DisplayZoneAndCoords))]
    [NotifyPropertyChangedFor(nameof(DisplayMapFullInfo))]
    [NotifyPropertyChangedFor(nameof(HasCoordinates))]
    private int? _charY;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayZone))]
    [NotifyPropertyChangedFor(nameof(DisplayZoneAndCoords))]
    [NotifyPropertyChangedFor(nameof(DisplayMapFullInfo))]
    private int? _currentPlayers;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayZone))]
    [NotifyPropertyChangedFor(nameof(DisplayZoneAndCoords))]
    [NotifyPropertyChangedFor(nameof(DisplayMapFullInfo))]
    private int? _maxPlayers;

    [ObservableProperty]
    private float _gameSpeed = 1.0f;

    public AccountItemViewModel(Account model)
    {
        Model = model;
        _gameSpeed = model.GameSpeed;
    }

    public string DisplayUsername => IsMasked ? TextMaskHelper.MaskText(Username) : Username;

    public string DisplayCharacterName => IsMasked ? TextMaskHelper.MaskText(CharacterName) : (CharacterName ?? string.Empty);

    public string StatusDisplayName
    {
        get
        {
            if (Status == GameSessionStatus.Offline && !string.IsNullOrWhiteSpace(StatusMessage) && StatusMessage.Contains("kết nối lại", StringComparison.OrdinalIgnoreCase))
            {
                return "Kết nối lại...";
            }

            return Status switch
            {
                GameSessionStatus.Active => "Online",
                GameSessionStatus.Running => "Đang mở",
                GameSessionStatus.LoggingIn => "Đang vào...",
                GameSessionStatus.Waiting => "Chờ...",
                GameSessionStatus.Offline => "Offline",
                GameSessionStatus.Banned => "Bị khóa",
                GameSessionStatus.Error => "Lỗi",
                _ => Status.ToString()
            };
        }
    }

    public string ActionMessage => LastActionReport.Message;
    public bool HasActionMessage => !string.IsNullOrWhiteSpace(ActionMessage);
    public bool IsActionFailed => LastActionReport.Status == ActionStatus.Failure;

    public string StatusTooltip
    {
        get
        {
            var baseStatus = !string.IsNullOrWhiteSpace(StatusMessage)
                ? $"{StatusDisplayName}: {StatusMessage}"
                : StatusDisplayName;

            if (HasActionMessage && ActionMessage != StatusMessage)
            {
                return $"{baseStatus}\nHành động: {ActionMessage}";
            }

            return baseStatus;
        }
    }

    public string GenderDisplayName => CharacterGender switch
    {
        0 => "TĐ",
        1 => "NM",
        2 => "XD",
        _ => ""
    };

    public bool HasCharacterName => !string.IsNullOrWhiteSpace(CharacterName);
    public bool HasHpMp => CharacterHP.HasValue || CharacterMP.HasValue;

    public string DisplayHp
    {
        get
        {
            if (!CharacterHP.HasValue) return "--";
            if (CharacterMaxHP.HasValue && CharacterMaxHP.Value > 0)
                return $"{CharacterHP.Value:N0}/{CharacterMaxHP.Value:N0}";
            return $"{CharacterHP.Value:N0}";
        }
    }

    public string DisplayMp
    {
        get
        {
            if (!CharacterMP.HasValue) return "--";
            if (CharacterMaxMP.HasValue && CharacterMaxMP.Value > 0)
                return $"{CharacterMP.Value:N0}/{CharacterMaxMP.Value:N0}";
            return $"{CharacterMP.Value:N0}";
        }
    }

    public bool HasMapInfo => !string.IsNullOrWhiteSpace(MapName) || (MapId.HasValue && MapId.Value >= 0);

    public string DisplayMapName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(MapName)) return MapName;
            if (MapId.HasValue && MapId.Value >= 0) return $"Map {MapId.Value}";
            return "--";
        }
    }

    public string DisplayZone
    {
        get
        {
            if (!ZoneId.HasValue || ZoneId.Value < 0) return string.Empty;
            if (CurrentPlayers.HasValue && MaxPlayers.HasValue && MaxPlayers.Value > 0)
            {
                return $"Khu {ZoneId.Value} ({CurrentPlayers.Value}/{MaxPlayers.Value})";
            }
            return $"Khu {ZoneId.Value}";
        }
    }

    public bool HasCoordinates => CharX.HasValue && CharY.HasValue;

    public string DisplayCoordinates => HasCoordinates ? $"({CharX}, {CharY})" : string.Empty;

    public string DisplayZoneAndCoords
    {
        get
        {
            if (!string.IsNullOrEmpty(DisplayZone) && !string.IsNullOrEmpty(DisplayCoordinates))
                return $"{DisplayZone}  {DisplayCoordinates}";
            if (!string.IsNullOrEmpty(DisplayZone))
                return DisplayZone;
            return DisplayCoordinates;
        }
    }

    public string DisplayMapFullInfo
    {
        get
        {
            if (!HasMapInfo) return "--";
            string baseInfo = !string.IsNullOrEmpty(DisplayZone) ? $"{DisplayMapName} - {DisplayZone}" : DisplayMapName;
            if (HasCoordinates) return $"{baseInfo} [X: {CharX}, Y: {CharY}]";
            return baseInfo;
        }
    }

    public bool IsRunning => Status != GameSessionStatus.Offline || ProcessId.HasValue;

    public void UpdateSnapshot(GameSessionSnapshot? snapshot)
    {
        if (snapshot != null)
        {
            var p = snapshot.Player;
            var w = snapshot.World;

            CharacterName = p.Name;
            CharacterPower = p.Power;
            CharacterHP = p.Hp;
            CharacterMaxHP = p.MaxHp;
            CharacterMP = p.Mp;
            CharacterMaxMP = p.MaxMp;
            CharacterGender = p.Gender;
            CharX = p.X;
            CharY = p.Y;

            MapId = w.MapId >= 0 ? w.MapId : null;
            ZoneId = w.ZoneId >= 0 ? w.ZoneId : null;
            MapName = !string.IsNullOrWhiteSpace(w.MapName) ? w.MapName : null;
            CurrentPlayers = w.CurrentPlayers > 0 ? w.CurrentPlayers : null;
            MaxPlayers = w.MaxPlayers > 0 ? w.MaxPlayers : null;
        }
        else
        {
            CharacterName = null;
            CharacterPower = null;
            CharacterHP = null;
            CharacterMaxHP = null;
            CharacterMP = null;
            CharacterMaxMP = null;
            CharacterGender = null;
            MapId = null;
            ZoneId = null;
            MapName = null;
            CharX = null;
            CharY = null;
            CurrentPlayers = null;
            MaxPlayers = null;
        }
    }

    public void UpdateStatus(GameSessionStatus newStatus, int? pid = null, string? message = null)
    {
        Status = newStatus;
        if (message != null) StatusMessage = message;
        if (pid.HasValue) ProcessId = pid.Value;
        if (newStatus == GameSessionStatus.Offline)
        {
            ProcessId = null;
            StatusMessage = string.Empty;
            LastActionReport = ActionExecutionReport.Idle();
            UpdateSnapshot(null);
        }
        else if (newStatus is GameSessionStatus.Error or GameSessionStatus.Banned && !string.IsNullOrWhiteSpace(message))
        {
            LastActionReport = new ActionExecutionReport("Lỗi", ActionStatus.Failure, message, DateTime.UtcNow);
        }
        OnPropertyChanged(nameof(StatusDisplayName));
        OnPropertyChanged(nameof(StatusTooltip));
    }

    public void UpdateActionReport(ActionExecutionReport report)
    {
        LastActionReport = report;
    }

    public void RefreshData()
    {
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(Password));
        OnPropertyChanged(nameof(Server));
        OnPropertyChanged(nameof(ServerId));
        OnPropertyChanged(nameof(AutoLogin));
        OnPropertyChanged(nameof(DisplayUsername));
        OnPropertyChanged(nameof(GameSpeed));
    }
}
