using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.GUI.Presentation.Shell;
using DragonBoyAccountManager.GUI.Services.Dialogs;
using DragonBoyAccountManager.GUI.Services.Engine;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.Presentation.Features.GameConfig;

public sealed partial class GameConfigTabViewModel : ObservableObject, IActivatableTab
{
    private static readonly (string Label, int Width, int Height, int Zoom)[] WindowSizeChoices =
    [
        ("1280 x 720 (HD / Zoom x2)", 1280, 720, 2),
        ("1024 x 600 (Chuẩn PC / Zoom x2)", 1024, 600, 2),
        ("960 x 540 (qHD / Zoom x2)", 960, 540, 2),
        ("800 x 600 (Chuẩn 4:3 / Zoom x2)", 800, 600, 2),
        ("720 x 320 (Chuẩn nhỏ / Zoom x1)", 720, 320, 1),
        ("640 x 480 (Zoom x1)", 640, 480, 1),
        ("512 x 384 (Nhỏ gọn / Zoom x1)", 512, 384, 1)
    ];

    public static readonly string[] WindowSizeOptions = WindowSizeChoices.Select(c => c.Label).ToArray();

    private readonly ISettingsService _settingsService;
    private readonly IGameEngineService _gameEngine;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    private bool _isPlaySound = true;

    [ObservableProperty]
    private bool _lowGraphic;

    [ObservableProperty]
    private bool _isPaintAura = true;

    [ObservableProperty]
    private bool _isPaintAura2 = true;

    [ObservableProperty]
    private bool _isPaintChatVip = true;

    [ObservableProperty]
    private int _zoomLevel = 2;

    [ObservableProperty]
    private bool _enablePickTab = true;

    [ObservableProperty]
    private string _selectedSizeOption = WindowSizeChoices[1].Label;

    partial void OnSelectedSizeOptionChanged(string value)
    {
        var match = WindowSizeChoices.FirstOrDefault(c => c.Label == value);
        if (match.Zoom > 0)
        {
            ZoomLevel = match.Zoom;
        }
    }

    public GameConfigTabViewModel(
        ISettingsService settingsService,
        IGameEngineService gameEngine,
        IDialogService dialogService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _gameEngine = gameEngine ?? throw new ArgumentNullException(nameof(gameEngine));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

        LoadFromSettings();
    }

    public void OnTabActivated()
    {
        LoadFromSettings();
    }

    public void OnTabDeactivated()
    {
    }

    private void LoadFromSettings()
    {
        var config = _settingsService.Current.GameConfig ?? new GameConfigOptions();
        IsPlaySound = config.IsPlaySound;
        LowGraphic = config.LowGraphic;
        IsPaintAura = config.IsPaintAura;
        IsPaintAura2 = config.IsPaintAura2;
        IsPaintChatVip = config.IsPaintChatVip;
        ZoomLevel = config.ZoomLevel > 0 ? config.ZoomLevel : 2;
        EnablePickTab = config.EnablePickTab;

        int curW = _settingsService.Current.WindowCustomWidth;
        int curH = _settingsService.Current.WindowCustomHeight;
        var match = WindowSizeChoices.FirstOrDefault(c => c.Width == curW && c.Height == curH);
        if (match.Width > 0)
        {
            SelectedSizeOption = match.Label;
        }
        else if (ZoomLevel == 1)
        {
            SelectedSizeOption = "720 x 320 (Chuẩn nhỏ / Zoom x1)";
        }
        else
        {
            SelectedSizeOption = "1024 x 600 (Chuẩn PC / Zoom x2)";
        }
    }

    public GameConfigOptions CreateOptions() => new()
    {
        IsPlaySound = IsPlaySound,
        LowGraphic = LowGraphic,
        IsPaintAura = IsPaintAura,
        IsPaintAura2 = IsPaintAura2,
        IsPaintChatVip = IsPaintChatVip,
        ZoomLevel = ZoomLevel,
        EnablePickTab = EnablePickTab
    };

    [RelayCommand]
    public async Task SaveConfigAsync()
    {
        var config = CreateOptions();
        _settingsService.Current.GameConfig = config;

        var match = WindowSizeChoices.FirstOrDefault(c => c.Label == SelectedSizeOption);
        if (match.Width > 0 && match.Height > 0)
        {
            _settingsService.Current.WindowCustomWidth = match.Width;
            _settingsService.Current.WindowCustomHeight = match.Height;
        }

        GameRmsStorage.SaveGameConfig(config);
        await _settingsService.SaveAsync();

        await BroadcastConfigToSessionsAsync(config);
        _dialogService.ShowInfo("Cấu hình Game", "Đã lưu cấu hình RMS và áp dụng thành công!");
    }

    private async Task<int> BroadcastConfigToSessionsAsync(GameConfigOptions config)
    {
        var sessionIds = _gameEngine.ActiveSessionIds.ToList();
        var command = new SetGameConfigCommand(config);

        int count = 0;
        foreach (var id in sessionIds)
        {
            var result = await _gameEngine.ExecuteCommandAsync(id, command);
            if (result.IsSuccess)
            {
                count++;
            }
        }

        return count;
    }
}
