using System;
using System.Collections.Generic;

namespace DragonBoyAccountManager.GUI.Models;

public sealed class AppSettings
{
    public string GamePath { get; set; } = string.Empty;
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectDelaySeconds { get; set; } = 3;
    public bool AutoArrangeWindows { get; set; }
    public string WindowArrangeMode { get; set; } = "Grid";
    public int WindowArrangeColumns { get; set; } = 0;
    public int WindowCustomWidth { get; set; } = 0;
    public int WindowCustomHeight { get; set; } = 0;
    public bool FastTeleport { get; set; } = false;
    public bool AutoAssignSkills { get; set; } = true;
    public Dictionary<string, string> CustomValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
