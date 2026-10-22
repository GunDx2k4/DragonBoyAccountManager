using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Settings.Items;

public partial class SettingGroupViewModel : ObservableObject
{
    public string Id { get; }
    public string Title { get; }
    public string? Description { get; }

    [ObservableProperty]
    private bool _hasToggle;

    [ObservableProperty]
    private bool _isEnabled = true;

    public ObservableCollection<SettingItemViewModel> Settings { get; } = [];

    public Action<SettingGroupViewModel>? OnToggleChanged { get; set; }

    public SettingGroupViewModel(
        string id,
        string title,
        string? description = null,
        bool hasToggle = false,
        bool isEnabled = true)
    {
        Id = id;
        Title = title;
        Description = description;
        HasToggle = hasToggle;
        IsEnabled = isEnabled;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnToggleChanged?.Invoke(this);
    }

    public SettingGroupViewModel Add(SettingItemViewModel setting)
    {
        Settings.Add(setting);
        return this;
    }

    public SettingItemViewModel? Find(string key)
    {
        return Settings.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public bool GetBool(string key, bool fallback = false)
    {
        var item = Find(key);
        return item != null ? item.BoolValue : fallback;
    }

    public int GetInt(string key, int fallback = 0)
    {
        var item = Find(key);
        return item != null ? item.NumberValue : fallback;
    }

    public string GetString(string key, string fallback = "")
    {
        var item = Find(key);
        return item != null ? item.GetValueAsString() : fallback;
    }
}
