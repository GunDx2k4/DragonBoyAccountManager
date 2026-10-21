using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DragonBoyAccountManager.GUI.Domain.Settings;

public sealed class AppSettings
{
    public string GamePath { get; set; } = string.Empty;
    public bool AutoReconnect { get; set; } = true;
    public int ReconnectDelaySeconds { get; set; } = 3;
    public bool AutoArrangeWindows { get; set; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WindowArrangeMode WindowArrangeMode { get; set; } = WindowArrangeMode.Grid;
    public int WindowArrangeColumns { get; set; } = 0;
    public int WindowCustomWidth { get; set; } = 0;
    public int WindowCustomHeight { get; set; } = 0;
    public bool FastTeleport { get; set; } = false;
    public bool AutoAssignSkills { get; set; } = true;
    public Dictionary<string, string> CustomValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
