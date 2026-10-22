using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Settings;

public sealed partial class SettingsTabViewModel : ObservableObject
{
    private static readonly (string Label, (int Width, int Height) Value)[] WindowSizeChoices =
    [
        ("Giữ nguyên kích thước", (0, 0)),
        ("1024 x 600 (Chuẩn PC)", (1024, 600)),
        ("720 x 320 (Chuẩn nhỏ / Zoom x1)", (720, 320)),
        ("800 x 600", (800, 600)),
        ("640 x 480", (640, 480)),
        ("512 x 384 (Nhỏ gọn)", (512, 384))
    ];

    private static readonly (string Label, WindowArrangeMode Value)[] ArrangeModeChoices =
    [
        ("Lưới tự động (Grid)", WindowArrangeMode.Grid),
        ("Chia đều kín màn hình (Tile)", WindowArrangeMode.Tile),
        ("Bậc thang (Cascade)", WindowArrangeMode.Cascade)
    ];

    private static readonly (string Label, int Value)[] ArrangeColumnChoices =
    [
        ("Tự động (Theo độ phân giải)", 0),
        ("2 Cột", 2),
        ("3 Cột", 3),
        ("4 Cột", 4),
        ("5 Cột", 5),
        ("6 Cột", 6)
    ];

    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;

    public ObservableCollection<SettingGroupViewModel> Groups { get; } = [];

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
        var cur = _settingsService.Current;

        Groups.Add(new SettingGroupViewModel(
            "network",
            "CẤU HÌNH TỰ ĐỘNG KẾT NỐI",
            "Thiết lập kết nối lại và điều tiết tần suất mở game")
            .Add(SettingItemViewModel.CreateBoolean(
                "AutoReconnect",
                "Tự động kết nối lại khi mất mạng",
                cur.AutoReconnect,
                "Tự động bấm đăng nhập lại khi game bị ngắt kết nối hoặc mất mạng.")
                .BindBool(s => s.AutoReconnect, (s, v) => s.AutoReconnect = v))
            .Add(SettingItemViewModel.CreateNumber(
                "ReconnectDelaySeconds",
                "Thời gian giãn cách mở game / Reconnect",
                cur.ReconnectDelaySeconds,
                1,
                10,
                "giây",
                "Giãn cách giữa các lần mở game hoặc thử kết nối lại.")
                .BindNumber(s => s.ReconnectDelaySeconds, (s, v) => s.ReconnectDelaySeconds = v)));

        Groups.Add(new SettingGroupViewModel(
            "window",
            "CẤU HÌNH CỬA SỔ & KÍCH THƯỚC GAME",
            "Thiết lập kích thước màn hình khi khởi động và bố cục sắp xếp")
            .Add(SettingItemViewModel.CreateSelect(
                "WindowSizePreset",
                "Kích thước cửa sổ khi mở game",
                WindowSizeChoices[0].Label,
                WindowSizeChoices.Select(c => c.Label),
                "Áp dụng kích thước này cho cửa sổ game ngay từ đầu khi vừa bật lên.")
                .BindSelect(
                    WindowSizeChoices,
                    s => (Width: s.WindowCustomWidth, Height: s.WindowCustomHeight),
                    (s, v) => { s.WindowCustomWidth = v.Width; s.WindowCustomHeight = v.Height; }))
            .Add(SettingItemViewModel.CreateBoolean(
                "AutoArrangeWindows",
                "Tự động sắp xếp cửa sổ khi mở game",
                cur.AutoArrangeWindows,
                "Sau khi mở game, tự động định vị các tab game (chỉ di chuyển vị trí, không đổi kích thước).")
                .BindBool(s => s.AutoArrangeWindows, (s, v) => s.AutoArrangeWindows = v))
            .Add(SettingItemViewModel.CreateSelect(
                "WindowArrangeMode",
                "Chế độ sắp xếp cửa sổ",
                ArrangeModeChoices[0].Label,
                ArrangeModeChoices.Select(c => c.Label),
                "Bố cục phân bổ vị trí các cửa sổ game trên màn hình.")
                .BindSelect(ArrangeModeChoices, s => s.WindowArrangeMode, (s, v) => s.WindowArrangeMode = v))
            .Add(SettingItemViewModel.CreateSelect(
                "WindowArrangeColumns",
                "Số cột hiển thị (Chế độ Lưới)",
                ArrangeColumnChoices[0].Label,
                ArrangeColumnChoices.Select(c => c.Label),
                "Số lượng tab game tối đa trên mỗi hàng ngang.")
                .BindSelect(ArrangeColumnChoices, s => s.WindowArrangeColumns, (s, v) => s.WindowArrangeColumns = v)));

        Groups.Add(new SettingGroupViewModel(
            "movement",
            "CẤU HÌNH DI CHUYỂN",
            "Thiết lập cơ chế di chuyển tìm đường thông minh A* hoặc dịch chuyển nhanh (Tele)")
            .Add(SettingItemViewModel.CreateBoolean(
                "FastTeleport",
                "Dịch chuyển nhanh (Teleport)",
                cur.FastTeleport,
                "Dịch chuyển tức thời đến tọa độ đích (bỏ qua tìm đường và di chuyển từng bước).")
                .BindBool(s => s.FastTeleport, (s, v) => s.FastTeleport = v)));

        Groups.Add(new SettingGroupViewModel(
            "skills",
            "CẤU HÌNH KỸ NĂNG & PHÍM TẮT",
            "Thiết lập tự động nạp và gán phím tắt cho tất cả kỹ năng")
            .Add(SettingItemViewModel.CreateBoolean(
                "AutoAssignSkills",
                "Tự động gán toàn bộ kỹ năng vào phím tắt (1 - 0)",
                cur.AutoAssignSkills,
                "Tự động nạp toàn bộ danh sách kỹ năng hiện có vào các ô phím tắt từ 1 đến 0 khi nhân vật vào game.")
                .BindBool(s => s.AutoAssignSkills, (s, v) => s.AutoAssignSkills = v)));
    }

    private void LoadValuesFromSettings()
    {
        var settings = _settingsService.Current;

        foreach (var group in Groups)
        {
            foreach (var item in group.Settings)
            {
                item.LoadFromSettings?.Invoke(settings);

                if (settings.CustomValues != null &&
                    settings.CustomValues.TryGetValue($"{group.Id}.{item.Key}", out var rawVal))
                {
                    item.SetValueFromString(rawVal);
                }
            }
        }
    }

    public SettingGroupViewModel? FindGroup(string id)
    {
        return Groups.FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public void RegisterGroup(SettingGroupViewModel group)
    {
        if (Groups.Any(g => string.Equals(g.Id, group.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (_settingsService.Current.CustomValues != null)
        {
            foreach (var item in group.Settings)
            {
                item.LoadFromSettings?.Invoke(_settingsService.Current);
                string fullKey = $"{group.Id}.{item.Key}";
                if (_settingsService.Current.CustomValues.TryGetValue(fullKey, out var rawVal))
                {
                    item.SetValueFromString(rawVal);
                }
            }
        }

        Groups.Add(group);
    }

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
        var settings = _settingsService.Current;
        settings.CustomValues ??= new(StringComparer.OrdinalIgnoreCase);

        foreach (var group in Groups)
        {
            if (group.HasToggle)
            {
                settings.CustomValues[$"{group.Id}.Enabled"] = group.IsEnabled.ToString();
            }

            foreach (var item in group.Settings)
            {
                item.ApplyToSettings?.Invoke(settings);
                settings.CustomValues[$"{group.Id}.{item.Key}"] = item.GetValueAsString();
            }
        }

        await _settingsService.SaveAsync();
        _dialogService.ShowInfo("Cài đặt", "Đã lưu cấu hình hệ thống thành công!");
    }
}
