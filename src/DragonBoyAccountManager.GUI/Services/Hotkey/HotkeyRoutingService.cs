using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragonBoyAccountManager.GUI.Common.Models;
using DragonBoyAccountManager.GUI.Services.Dialog;
using DragonBoyAccountManager.GUI.ViewModels;

namespace DragonBoyAccountManager.GUI.Services.Hotkey;

public sealed class HotkeyRoutingService(AccountFormViewModel accountForm, IDialogService dialogService) : IHotkeyRoutingService
{
    private readonly AccountFormViewModel _accountForm = accountForm;
    private readonly IDialogService _dialogService = dialogService;
    private readonly HashSet<Key> _heldMovementKeys = [];

    public bool HandlePreviewKeyDown(KeyEventArgs e, Window? ownerWindow = null)
    {
        var focused = Keyboard.FocusedElement;
        if (focused is TextBox or PasswordBox or RichTextBox)
            return false;
        if (e.OriginalSource is TextBox or PasswordBox or RichTextBox)
            return false;
        if (focused is ComboBox cb && cb.IsDropDownOpen)
            return false;

        var movementKey = GetMovementKey(e.Key);
        if (movementKey.HasValue)
        {
            if (!_heldMovementKeys.Contains(e.Key))
            {
                _heldMovementKeys.Add(e.Key);
                _accountForm.StartHoldingKey(movementKey.Value);
            }
            return true;
        }

        if (e.Key == Key.Enter)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionOkAsync();
            }
            return true;
        }

        if (e.Key == Key.Space)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionUsePeanAsync();
            }
            return true;
        }

        if (e.Key == Key.Tab)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionFocusAsync();
            }
            return true;
        }

        if (e.Key == Key.Q)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionPickItemAsync();
            }
            return true;
        }

        if (e.Key == Key.F1)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionShowPanelAsync();
            }
            return true;
        }

        if (e.Key == Key.F2)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionHidePanelAsync();
            }
            return true;
        }

        if (e.Key == Key.F12)
        {
            if (!e.IsRepeat)
            {
                _dialogService.ShowHotkeyCheatsheet(ownerWindow);
            }
            return true;
        }

        if (e.Key == Key.Y)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionYesAsync();
            }
            return true;
        }

        if (e.Key == Key.R)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionChatAsync();
            }
            return true;
        }

        if (e.Key is Key.Back or Key.Escape)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.ActionBackAsync();
            }
            return true;
        }

        if (e.Key == Key.C)
        {
            _accountForm.UseCapsule();
            return true;
        }

        if (e.Key == Key.X)
        {
            _accountForm.UseTdlt();
            return true;
        }

        bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        if (isShift && e.Key == Key.D2)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.SendRawKeyPulseAsync(64);
            }
            return true;
        }

        if (e.Key is Key.OemMinus or Key.Subtract)
        {
            if (!e.IsRepeat)
            {
                int code = isShift ? 95 : 45;
                _ = _accountForm.SendRawKeyPulseAsync(code);
            }
            return true;
        }

        if (e.Key == Key.OemPlus && !isShift)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.SendRawKeyPulseAsync(-25);
            }
            return true;
        }

        if (e.Key is Key.OemPeriod or Key.Decimal)
        {
            if (!e.IsRepeat)
            {
                _ = _accountForm.SendRawKeyPulseAsync(46);
            }
            return true;
        }

        int? skillSlot = e.Key switch
        {
            Key.D1 or Key.NumPad1 => 0,
            Key.D2 or Key.NumPad2 => 1,
            Key.D3 or Key.NumPad3 => 2,
            Key.D4 or Key.NumPad4 => 3,
            Key.D5 or Key.NumPad5 => 4,
            Key.D6 or Key.NumPad6 => 5,
            Key.D7 or Key.NumPad7 => 6,
            Key.D8 or Key.NumPad8 => 7,
            Key.D9 or Key.NumPad9 => 8,
            Key.D0 or Key.NumPad0 => 9,
            _ => null
        };

        if (skillSlot.HasValue)
        {
            _accountForm.UseSkillCommand.Execute(skillSlot.Value);
            return true;
        }

        if (e.Key == Key.OemOpenBrackets)
        {
            _accountForm.PreviousZoneCommand.Execute(null);
            return true;
        }

        if (e.Key == Key.OemCloseBrackets)
        {
            _accountForm.NextZoneCommand.Execute(null);
            return true;
        }

        if (e.Key >= Key.A && e.Key <= Key.Z)
        {
            if (!e.IsRepeat)
            {
                int ascii = isShift ? ((int)'A' + (e.Key - Key.A)) : ((int)'a' + (e.Key - Key.A));
                _ = _accountForm.SendRawKeyPulseAsync(ascii);
            }
            return true;
        }

        return false;
    }

    public bool HandlePreviewKeyUp(KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement;
        if (focused is TextBox or PasswordBox or RichTextBox)
            return false;
        if (e.OriginalSource is TextBox or PasswordBox or RichTextBox)
            return false;

        var movementKey = GetMovementKey(e.Key);
        if (movementKey.HasValue)
        {
            _heldMovementKeys.Remove(e.Key);
            _accountForm.StopHoldingKey(movementKey.Value);
            return true;
        }

        return false;
    }

    public void ReleaseAllHeldKeys()
    {
        if (_heldMovementKeys.Count == 0) return;
        foreach (var key in _heldMovementKeys.ToList())
        {
            var movementKey = GetMovementKey(key);
            if (movementKey.HasValue)
            {
                _accountForm.StopHoldingKey(movementKey.Value);
            }
        }
        _heldMovementKeys.Clear();
    }

    private static GameKey? GetMovementKey(Key key) => key switch
    {
        Key.Up or Key.W => GameKey.Up,
        Key.Down or Key.S => GameKey.Down,
        Key.Left or Key.A => GameKey.Left,
        Key.Right or Key.D => GameKey.Right,
        _ => null
    };
}
