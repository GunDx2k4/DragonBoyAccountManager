using System;
using System.Collections.Generic;

namespace DragonBoyAccountManager.GUI.Models;

public sealed class AppSettings
{
    public string GamePath { get; set; } = string.Empty;
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectDelaySeconds { get; set; } = 3;
    public bool AutoArrangeWindows { get; set; }
    public string WindowArrangeMode { get; set; } = "Grid"; // Grid, Tile, Cascade
    public int WindowArrangeColumns { get; set; } = 0; // 0 = Tự động
    public int WindowCustomWidth { get; set; } = 0; // 0 = Giữ nguyên
    public int WindowCustomHeight { get; set; } = 0; // 0 = Giữ nguyên
    public bool FastTeleport { get; set; } = false; // Chế độ dịch chuyển nhanh (teleport) đến mục tiêu
    public Dictionary<string, string> CustomValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
