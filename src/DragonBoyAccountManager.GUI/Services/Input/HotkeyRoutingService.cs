using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DragonBoyAccountManager.GUI.Services.Input;

public sealed class HotkeyRoutingService(IGameInputService gameInput, IDialogService dialogService) : IHotkeyRoutingService
{
    private readonly IGameInputService _gameInput = gameInput;
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
                _gameInput.StartHoldingKey(movementKey.Value);
            }
            return true;
        }

        if (e.Key == Key.Enter)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.ActionOkAsync();
            }
            return true;
        }

        if (e.Key == Key.Space)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.ActionUsePeanAsync();
            }
            return true;
        }

        if (e.Key == Key.Tab)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.ActionFocusAsync();
            }
            return true;
        }

        if (e.Key == Key.Q)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.ActionPickItemAsync();
            }
            return true;
        }

        if (e.Key == Key.F1)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.ActionShowPanelAsync();
            }
            return true;
        }

        if (e.Key == Key.F2)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.ActionHidePanelAsync();
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
                _ = _gameInput.ActionYesAsync();
            }
            return true;
        }

        if (e.Key == Key.R)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.ActionChatAsync();
            }
            return true;
        }

        if (e.Key is Key.Back or Key.Escape)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.ActionBackAsync();
            }
            return true;
        }

        if (e.Key == Key.C)
        {
            _gameInput.UseCapsule();
            return true;
        }

        if (e.Key == Key.X)
        {
            _gameInput.UseTdlt();
            return true;
        }

        bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        if (isShift && e.Key == Key.D2)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.SendRawKeyPulseAsync(64);
            }
            return true;
        }

        if (e.Key is Key.OemMinus or Key.Subtract)
        {
            if (!e.IsRepeat)
            {
                int code = isShift ? 95 : 45;
                _ = _gameInput.SendRawKeyPulseAsync(code);
            }
            return true;
        }

        if (e.Key == Key.OemPlus && !isShift)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.SendRawKeyPulseAsync(-25);
            }
            return true;
        }

        if (e.Key is Key.OemPeriod or Key.Decimal)
        {
            if (!e.IsRepeat)
            {
                _ = _gameInput.SendRawKeyPulseAsync(46);
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
            _gameInput.UseSkill(skillSlot.Value);
            return true;
        }

        if (e.Key == Key.OemOpenBrackets)
        {
            _gameInput.PreviousZone();
            return true;
        }

        if (e.Key == Key.OemCloseBrackets)
        {
            _gameInput.NextZone();
            return true;
        }

        if (e.Key >= Key.A && e.Key <= Key.Z)
        {
            if (!e.IsRepeat)
            {
                int ascii = isShift ? ((int)'A' + (e.Key - Key.A)) : ((int)'a' + (e.Key - Key.A));
                _ = _gameInput.SendRawKeyPulseAsync(ascii);
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
            _gameInput.StopHoldingKey(movementKey.Value);
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
                _gameInput.StopHoldingKey(movementKey.Value);
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
