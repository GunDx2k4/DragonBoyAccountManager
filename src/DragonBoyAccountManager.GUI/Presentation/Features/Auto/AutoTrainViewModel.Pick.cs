using CommunityToolkit.Mvvm.Input;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Auto;

public sealed partial class AutoTrainViewModel
{
    [RelayCommand]
    public void AddFilterPickPreset(string? presetIds)
    {
        if (string.IsNullOrWhiteSpace(presetIds))
        {
            FilterPickIds = string.Empty;
            return;
        }

        var currentTokens = FilterPickIds.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var newTokens = presetIds.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (int i = 0; i < newTokens.Length; i++)
        {
            var token = newTokens[i];
            if (!currentTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
            {
                currentTokens.Add(token);
            }
        }
        FilterPickIds = string.Join(",", currentTokens);
    }

    [RelayCommand]
    public void ClearFilterPickIds()
    {
        FilterPickIds = string.Empty;
    }

    private void UpdatePickStatus()
    {
        if (!AutoPick)
        {
            PickStatusText = "Chưa bật tự động nhặt";
            return;
        }

        var parts = new List<string>(3);
        if (PickAllMap)
        {
            parts.Add("Toàn map");
        }
        if (PickOthersItems)
        {
            parts.Add("Đồ người khác");
        }
        if (!string.IsNullOrWhiteSpace(FilterPickIds))
        {
            parts.Add($"Lọc {FilterPickIds.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries).Length} ID");
        }

        PickStatusText = parts.Count > 0
            ? $"Đang nhặt: {string.Join(", ", parts)}"
            : "Đang nhặt: Mặc định (trong tầm)";
    }
}
