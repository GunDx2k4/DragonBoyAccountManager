using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DragonBoyAccountManager.GUI.Presentation.Features.Settings.Items;

public partial class SettingItemViewModel : ObservableObject
{
    public string Key { get; }
    public string Label { get; }
    public string? Description { get; }
    public SettingViewType ViewType { get; }

    [ObservableProperty]
    private bool _boolValue;

    [ObservableProperty]
    private int _numberValue;

    [ObservableProperty]
    private string _textValue = string.Empty;

    [ObservableProperty]
    private string _selectedOption = string.Empty;

    public ObservableCollection<string> Options { get; } = [];

    public int Min { get; set; } = 0;
    public int Max { get; set; } = 100;
    public string Unit { get; set; } = string.Empty;
    public int MaxLength { get; set; } = 255;

    public Action<SettingItemViewModel>? OnChanged { get; set; }
    public Action<AppSettings>? LoadFromSettings { get; set; }
    public Action<AppSettings>? ApplyToSettings { get; set; }

    public SettingItemViewModel(string key, string label, SettingViewType viewType, string? description = null)
    {
        Key = key;
        Label = label;
        ViewType = viewType;
        Description = description;
    }

    partial void OnBoolValueChanged(bool value) => OnChanged?.Invoke(this);
    partial void OnNumberValueChanged(int value) => OnChanged?.Invoke(this);
    partial void OnTextValueChanged(string value) => OnChanged?.Invoke(this);
    partial void OnSelectedOptionChanged(string value) => OnChanged?.Invoke(this);

    public string GetValueAsString()
    {
        return ViewType switch
        {
            SettingViewType.Boolean => BoolValue.ToString(),
            SettingViewType.Number => NumberValue.ToString(CultureInfo.InvariantCulture),
            SettingViewType.Text => TextValue,
            SettingViewType.Select => SelectedOption,
            _ => string.Empty
        };
    }

    public void SetValueFromString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return;

        switch (ViewType)
        {
            case SettingViewType.Boolean:
                if (bool.TryParse(raw, out var b)) BoolValue = b;
                break;
            case SettingViewType.Number:
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                    NumberValue = Math.Clamp(n, Min, Max);
                break;
            case SettingViewType.Text:
                TextValue = raw;
                break;
            case SettingViewType.Select:
                if (Options.Contains(raw)) SelectedOption = raw;
                break;
        }
    }

    public static SettingItemViewModel CreateBoolean(
        string key,
        string label,
        bool defaultValue,
        string? description = null,
        Action<bool>? onChanged = null)
    {
        var item = new SettingItemViewModel(key, label, SettingViewType.Boolean, description)
        {
            BoolValue = defaultValue
        };
        if (onChanged != null)
        {
            item.OnChanged = vm => onChanged(vm.BoolValue);
        }
        return item;
    }

    public static SettingItemViewModel CreateNumber(
        string key,
        string label,
        int defaultValue,
        int min,
        int max,
        string unit = "",
        string? description = null,
        Action<int>? onChanged = null)
    {
        var item = new SettingItemViewModel(key, label, SettingViewType.Number, description)
        {
            Min = min,
            Max = max,
            Unit = unit,
            NumberValue = defaultValue
        };
        if (onChanged != null)
        {
            item.OnChanged = vm => onChanged(vm.NumberValue);
        }
        return item;
    }

    public static SettingItemViewModel CreateText(
        string key,
        string label,
        string defaultValue,
        string? description = null,
        int maxLength = 255,
        Action<string>? onChanged = null)
    {
        var item = new SettingItemViewModel(key, label, SettingViewType.Text, description)
        {
            MaxLength = maxLength,
            TextValue = defaultValue
        };
        if (onChanged != null)
        {
            item.OnChanged = vm => onChanged(vm.TextValue);
        }
        return item;
    }

    public static SettingItemViewModel CreateSelect(
        string key,
        string label,
        string defaultValue,
        IEnumerable<string> options,
        string? description = null,
        Action<string>? onChanged = null)
    {
        var item = new SettingItemViewModel(key, label, SettingViewType.Select, description);
        foreach (var opt in options)
        {
            item.Options.Add(opt);
        }
        item.SelectedOption = item.Options.Contains(defaultValue) ? defaultValue : (item.Options.Count > 0 ? item.Options[0] : string.Empty);

        if (onChanged != null)
        {
            item.OnChanged = vm => onChanged(vm.SelectedOption);
        }
        return item;
    }

    public SettingItemViewModel BindBool(Func<AppSettings, bool> getter, Action<AppSettings, bool> setter)
    {
        LoadFromSettings = s => BoolValue = getter(s);
        ApplyToSettings = s => setter(s, BoolValue);
        return this;
    }

    public SettingItemViewModel BindNumber(Func<AppSettings, int> getter, Action<AppSettings, int> setter)
    {
        LoadFromSettings = s => NumberValue = getter(s);
        ApplyToSettings = s => setter(s, NumberValue);
        return this;
    }

    public SettingItemViewModel BindSelect<T>(
        IReadOnlyList<(string Label, T Value)> choices,
        Func<AppSettings, T> getter,
        Action<AppSettings, T> setter)
    {
        Options.Clear();
        foreach (var (label, _) in choices)
        {
            Options.Add(label);
        }

        LoadFromSettings = s =>
        {
            var current = getter(s);
            var matched = choices.FirstOrDefault(c => EqualityComparer<T>.Default.Equals(c.Value, current));
            SelectedOption = matched.Label ?? (choices.Count > 0 ? choices[0].Label : string.Empty);
        };

        ApplyToSettings = s =>
        {
            var matched = choices.FirstOrDefault(c => c.Label == SelectedOption);
            setter(s, matched.Label != null ? matched.Value : choices[0].Value);
        };

        return this;
    }
}
