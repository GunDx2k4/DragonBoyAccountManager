using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.ViewModels;

namespace DragonBoyAccountManager.GUI.Views;

public partial class AccountConfigTabView : UserControl
{
    private readonly ConcurrentDictionary<GameKey, long> _pressTimestamps = new();

    public AccountConfigTabView()
    {
        InitializeComponent();
    }

    private AccountFormViewModel? ViewModel => DataContext as AccountFormViewModel;

    private void DpadButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel != null && sender is FrameworkElement elem && elem.Tag is string keyStr && Enum.TryParse<GameKey>(keyStr, out var key))
        {
            _pressTimestamps[key] = Stopwatch.GetTimestamp();
            ViewModel.StartHoldingKey(key);
        }
    }

    private async void DpadButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel != null && sender is FrameworkElement elem && elem.Tag is string keyStr && Enum.TryParse<GameKey>(keyStr, out var key))
        {
            if (_pressTimestamps.TryRemove(key, out long startTimestamp))
            {
                var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
                if (elapsedMs < 100)
                {
                    await Task.Delay(100 - (int)elapsedMs);
                }
            }
            ViewModel.StopHoldingKey(key);
        }
    }

    private void DpadButton_MouseLeave(object sender, MouseEventArgs e)
    {
        if (ViewModel != null && sender is FrameworkElement elem && elem.Tag is string keyStr && Enum.TryParse<GameKey>(keyStr, out var key))
        {
            _pressTimestamps.TryRemove(key, out _);
            ViewModel.StopHoldingKey(key);
        }
    }

    private void ZoneComboBox_DropDownOpened(object? sender, EventArgs e)
    {
        ViewModel?.RequestZoneList();
    }

    private void SkillSlot_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement elem && elem.DataContext is SkillButtonItem item)
        {
            e.Handled = true;
            ViewModel?.UseSkill(item);
        }
    }
}
