using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DragonBoyAccountManager.GUI.Services.Dialog;
using DragonBoyAccountManager.GUI.Services.Settings;

namespace DragonBoyAccountManager.GUI.ViewModels;

public sealed partial class SettingsTabViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;

    public ObservableCollection<SettingGroupViewModel> Groups { get; } = [];

    // Tương thích ngược với các thuộc tính cũ
    public bool AutoReconnect
    {
        get => FindGroup("network")?.GetBool("AutoReconnect", _settingsService.Current.AutoReconnect) ?? _settingsService.Current.AutoReconnect;
        set
        {
            var item = FindGroup("network")?.Find("AutoReconnect");
            if (item != null) item.BoolValue = value;
            OnPropertyChanged();
        }
    }

    public int ReconnectDelaySeconds
    {
        get => FindGroup("network")?.GetInt("ReconnectDelaySeconds", _settingsService.Current.ReconnectDelaySeconds) ?? _settingsService.Current.ReconnectDelaySeconds;
        set
        {
            var item = FindGroup("network")?.Find("ReconnectDelaySeconds");
            if (item != null) item.NumberValue = value;
            OnPropertyChanged();
        }
    }

    public bool AutoArrangeWindows
    {
        get => FindGroup("window")?.GetBool("AutoArrangeWindows", _settingsService.Current.AutoArrangeWindows) ?? _settingsService.Current.AutoArrangeWindows;
        set
        {
            var item = FindGroup("window")?.Find("AutoArrangeWindows");
            if (item != null) item.BoolValue = value;
            OnPropertyChanged();
        }
    }

    public SettingsTabViewModel(ISettingsService settingsService, IDialogService dialogService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;

        InitializeDefaultGroups();
        LoadValuesFromSettings();
    }

    private void InitializeDefaultGroups()
    {
        Groups.Clear();

        // 1. Nhóm Cấu hình Kết nối Mạng & Đăng nhập
        var networkGroup = new SettingGroupViewModel(
            "network",
            "CẤU HÌNH TỰ ĐỘNG KẾT NỐI",
            "Thiết lập kết nối lại và điều tiết tần suất mở game")
            .Add(SettingItemViewModel.CreateBoolean(
                "AutoReconnect",
                "Tự động kết nối lại khi mất mạng",
                _settingsService.Current.AutoReconnect,
                "Tự động bấm đăng nhập lại khi game bị ngắt kết nối hoặc mất mạng."))
            .Add(SettingItemViewModel.CreateNumber(
                "ReconnectDelaySeconds",
                "Thời gian giãn cách mở game / Reconnect",
                _settingsService.Current.ReconnectDelaySeconds,
                1,
                10,
                "giây",
                "Giãn cách giữa các lần mở game hoặc thử kết nối lại."));

        Groups.Add(networkGroup);

        // 2. Nhóm Cấu hình Cửa sổ Game
        var windowGroup = new SettingGroupViewModel(
            "window",
            "CẤU HÌNH CỬA SỔ GAME",
            "Thiết lập bố cục hiển thị và kích thước tab game trên màn hình")
            .Add(SettingItemViewModel.CreateBoolean(
                "AutoArrangeWindows",
                "Tự động sắp xếp cửa sổ khi mở game",
                _settingsService.Current.AutoArrangeWindows,
                "Sau khi khởi động game thành công, tự động định vị các tab game theo bố cục đã cấu hình."))
            .Add(SettingItemViewModel.CreateSelect(
                "WindowArrangeMode",
                "Chế độ sắp xếp cửa sổ",
                GetArrangeModeDisplay(_settingsService.Current.WindowArrangeMode),
                new string[] { "Lưới tự động (Grid)", "Chia đều kín màn hình (Tile)", "Bậc thang (Cascade)" },
                "Bố cục phân bổ vị trí các cửa sổ game trên màn hình máy tính."))
            .Add(SettingItemViewModel.CreateSelect(
                "WindowArrangeColumns",
                "Số cột hiển thị (Chế độ Lưới)",
                GetColumnsDisplay(_settingsService.Current.WindowArrangeColumns),
                new string[] { "Tự động (Theo độ phân giải)", "2 Cột", "3 Cột", "4 Cột", "5 Cột", "6 Cột" },
                "Số lượng tab game tối đa trên mỗi hàng ngang."))
            .Add(SettingItemViewModel.CreateSelect(
                "WindowSizePreset",
                "Kích thước cửa sổ khi sắp xếp",
                GetSizePresetDisplay(_settingsService.Current.WindowCustomWidth, _settingsService.Current.WindowCustomHeight),
                new string[] { "Giữ nguyên kích thước", "1024 x 600 (Chuẩn PC)", "720 x 320 (Chuẩn nhỏ / Zoom x1)", "800 x 600", "640 x 480", "512 x 384 (Nhỏ gọn)" },
                "Tự động đặt lại kích thước cửa sổ game khi sắp xếp."));

        Groups.Add(windowGroup);
    }

    private void LoadValuesFromSettings()
    {
        var settings = _settingsService.Current;

        var netGroup = FindGroup("network");
        if (netGroup != null)
        {
            var autoRec = netGroup.Find("AutoReconnect");
            if (autoRec != null) autoRec.BoolValue = settings.AutoReconnect;

            var delay = netGroup.Find("ReconnectDelaySeconds");
            if (delay != null) delay.NumberValue = settings.ReconnectDelaySeconds;
        }

        var winGroup = FindGroup("window");
        if (winGroup != null)
        {
            var autoArr = winGroup.Find("AutoArrangeWindows");
            if (autoArr != null) autoArr.BoolValue = settings.AutoArrangeWindows;

            var mode = winGroup.Find("WindowArrangeMode");
            if (mode != null) mode.SelectedOption = GetArrangeModeDisplay(settings.WindowArrangeMode);

            var cols = winGroup.Find("WindowArrangeColumns");
            if (cols != null) cols.SelectedOption = GetColumnsDisplay(settings.WindowArrangeColumns);

            var size = winGroup.Find("WindowSizePreset");
            if (size != null) size.SelectedOption = GetSizePresetDisplay(settings.WindowCustomWidth, settings.WindowCustomHeight);
        }

        // Tải các giá trị tuỳ biến của Action trong tương lai
        if (settings.CustomValues != null)
        {
            foreach (var group in Groups)
            {
                foreach (var item in group.Settings)
                {
                    string fullKey = $"{group.Id}.{item.Key}";
                    if (settings.CustomValues.TryGetValue(fullKey, out var rawVal))
                    {
                        item.SetValueFromString(rawVal);
                    }
                    else if (settings.CustomValues.TryGetValue(item.Key, out var shortVal))
                    {
                        item.SetValueFromString(shortVal);
                    }
                }
            }
        }
    }

    private static string GetArrangeModeDisplay(string? mode) => mode?.ToLowerInvariant() switch
    {
        "tile" => "Chia đều kín màn hình (Tile)",
        "cascade" => "Bậc thang (Cascade)",
        _ => "Lưới tự động (Grid)"
    };

    private static string GetColumnsDisplay(int cols) => cols switch
    {
        2 => "2 Cột",
        3 => "3 Cột",
        4 => "4 Cột",
        5 => "5 Cột",
        6 => "6 Cột",
        _ => "Tự động (Theo độ phân giải)"
    };

    private static string GetSizePresetDisplay(int w, int h) => (w, h) switch
    {
        (1024, 600) => "1024 x 600 (Chuẩn PC)",
        (720, 320) => "720 x 320 (Chuẩn nhỏ / Zoom x1)",
        (800, 600) => "800 x 600",
        (640, 480) => "640 x 480",
        (512, 384) => "512 x 384 (Nhỏ gọn)",
        _ => "Giữ nguyên kích thước"
    };

    public SettingGroupViewModel? FindGroup(string id)
    {
        return Groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Đăng ký thêm một nhóm cấu hình mới vào Tab Settings
    /// </summary>
    public void RegisterGroup(SettingGroupViewModel group)
    {
        if (Groups.Any(g => string.Equals(g.Id, group.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        // Kiểm tra xem đã có giá trị lưu từ trước cho nhóm này không
        if (_settingsService.Current.CustomValues != null)
        {
            foreach (var item in group.Settings)
            {
                string fullKey = $"{group.Id}.{item.Key}";
                if (_settingsService.Current.CustomValues.TryGetValue(fullKey, out var rawVal))
                {
                    item.SetValueFromString(rawVal);
                }
            }
        }

        Groups.Add(group);
    }

    /// <summary>
    /// Đăng ký nhanh một Action mới với nút Bật/Tắt và các tuỳ chọn con
    /// </summary>
    public SettingGroupViewModel RegisterAction(
        string actionId,
        string actionTitle,
        string? description = null,
        bool defaultEnabled = false,
        Action<SettingGroupViewModel>? configure = null)
    {
        var existing = FindGroup(actionId);
        if (existing != null)
        {
            return existing;
        }

        // Lấy trạng thái Bật/Tắt đã lưu (nếu có)
        bool isEnabled = defaultEnabled;
        if (_settingsService.Current.CustomValues != null &&
            _settingsService.Current.CustomValues.TryGetValue($"{actionId}.Enabled", out var rawEnabled) &&
            bool.TryParse(rawEnabled, out var parsedEnabled))
        {
            isEnabled = parsedEnabled;
        }

        var group = new SettingGroupViewModel(actionId, actionTitle, description, hasToggle: true, isEnabled: isEnabled);
        configure?.Invoke(group);

        RegisterGroup(group);
        return group;
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var netGroup = FindGroup("network");
        if (netGroup != null)
        {
            _settingsService.Current.AutoReconnect = netGroup.GetBool("AutoReconnect", _settingsService.Current.AutoReconnect);
            _settingsService.Current.ReconnectDelaySeconds = netGroup.GetInt("ReconnectDelaySeconds", _settingsService.Current.ReconnectDelaySeconds);
        }

        var winGroup = FindGroup("window");
        if (winGroup != null)
        {
            _settingsService.Current.AutoArrangeWindows = winGroup.GetBool("AutoArrangeWindows", _settingsService.Current.AutoArrangeWindows);

            var modeStr = winGroup.GetString("WindowArrangeMode", "Lưới tự động (Grid)");
            _settingsService.Current.WindowArrangeMode = modeStr switch
            {
                "Chia đều kín màn hình (Tile)" => "Tile",
                "Bậc thang (Cascade)" => "Cascade",
                _ => "Grid"
            };

            var colStr = winGroup.GetString("WindowArrangeColumns", "Tự động");
            _settingsService.Current.WindowArrangeColumns = colStr switch
            {
                "2 Cột" => 2,
                "3 Cột" => 3,
                "4 Cột" => 4,
                "5 Cột" => 5,
                "6 Cột" => 6,
                _ => 0
            };

            var sizeStr = winGroup.GetString("WindowSizePreset", "Giữ nguyên");
            var (w, h) = sizeStr switch
            {
                "1024 x 600 (Chuẩn PC)" => (1024, 600),
                "720 x 320 (Chuẩn nhỏ / Zoom x1)" => (720, 320),
                "800 x 600" => (800, 600),
                "640 x 480" => (640, 480),
                "512 x 384 (Nhỏ gọn)" => (512, 384),
                _ => (0, 0)
            };
            _settingsService.Current.WindowCustomWidth = w;
            _settingsService.Current.WindowCustomHeight = h;
        }

        // Lưu toàn bộ các cấu hình của các nhóm / action vào CustomValues
        _settingsService.Current.CustomValues ??= new(StringComparer.OrdinalIgnoreCase);

        foreach (var group in Groups)
        {
            if (group.HasToggle)
            {
                _settingsService.Current.CustomValues[$"{group.Id}.Enabled"] = group.IsEnabled.ToString();
            }

            foreach (var item in group.Settings)
            {
                _settingsService.Current.CustomValues[$"{group.Id}.{item.Key}"] = item.GetValueAsString();
            }
        }

        await _settingsService.SaveAsync();
        _dialogService.ShowInfo("Cài đặt", "Đã lưu cấu hình hệ thống thành công!");
    }
}
