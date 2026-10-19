using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DragonBoyAccountManager.GUI.Models;
using DragonBoyAccountManager.GUI.ViewModels;

namespace DragonBoyAccountManager.GUI.Views;

public partial class MainWindow : Window
{
    private readonly HashSet<Key> _heldMovementKeys = [];

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement;
        if (focused is TextBox or PasswordBox or RichTextBox)
            return;
        if (e.OriginalSource is TextBox or PasswordBox or RichTextBox)
            return;
        if (focused is ComboBox cb && cb.IsDropDownOpen)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        var movementKey = GetMovementKey(e.Key);

        if (movementKey.HasValue)
        {
            if (!_heldMovementKeys.Contains(e.Key))
            {
                _heldMovementKeys.Add(e.Key);
                vm.AccountForm.StartHoldingKey(movementKey.Value);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionOkAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionUsePeanAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionFocusAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Q)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionPickItemAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F1)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionShowPanelAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F2)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionHidePanelAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F12)
        {
            if (!e.IsRepeat)
            {
                var cheatsheet = new HotkeyCheatsheetWindow
                {
                    Owner = this
                };
                cheatsheet.ShowDialog();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Y)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionYesAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.R)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionChatAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Back or Key.Escape)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.ActionBackAsync();
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C)
        {
            vm.AccountForm.UseCapsule();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.X)
        {
            vm.AccountForm.UseTdlt();
            e.Handled = true;
            return;
        }

        bool isShift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
        if (isShift && e.Key == Key.D2)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.SendRawKeyPulseAsync(64);
            }
            e.Handled = true;
            return;
        }

        if (e.Key is Key.OemMinus or Key.Subtract)
        {
            if (!e.IsRepeat)
            {
                int code = isShift ? 95 : 45;
                _ = vm.AccountForm.SendRawKeyPulseAsync(code);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemPlus && !isShift)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.SendRawKeyPulseAsync(-25);
            }
            e.Handled = true;
            return;
        }

        if (e.Key is Key.OemPeriod or Key.Decimal)
        {
            if (!e.IsRepeat)
            {
                _ = vm.AccountForm.SendRawKeyPulseAsync(46);
            }
            e.Handled = true;
            return;
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
            vm.AccountForm.UseSkillCommand.Execute(skillSlot.Value);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemOpenBrackets)
        {
            vm.AccountForm.PreviousZoneCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.OemCloseBrackets)
        {
            vm.AccountForm.NextZoneCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.Key >= Key.A && e.Key <= Key.Z)
        {
            if (!e.IsRepeat)
            {
                int ascii = isShift ? ((int)'A' + (e.Key - Key.A)) : ((int)'a' + (e.Key - Key.A));
                _ = vm.AccountForm.SendRawKeyPulseAsync(ascii);
            }
            e.Handled = true;
            return;
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        var focused = Keyboard.FocusedElement;
        if (focused is TextBox or PasswordBox or RichTextBox)
            return;
        if (e.OriginalSource is TextBox or PasswordBox or RichTextBox)
            return;

        if (DataContext is not MainViewModel vm)
            return;

        var movementKey = GetMovementKey(e.Key);
        if (movementKey.HasValue)
        {
            _heldMovementKeys.Remove(e.Key);
            vm.AccountForm.StopHoldingKey(movementKey.Value);
            e.Handled = true;
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        ReleaseAllHeldKeys();
    }

    private void ReleaseAllHeldKeys()
    {
        if (_heldMovementKeys.Count == 0) return;
        if (DataContext is MainViewModel vm)
        {
            foreach (var key in _heldMovementKeys.ToList())
            {
                var movementKey = GetMovementKey(key);
                if (movementKey.HasValue)
                {
                    vm.AccountForm.StopHoldingKey(movementKey.Value);
                }
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_GETMINMAXINFO:
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
                break;

            case WM_NCHITTEST:
                if (HandleNcHitTest(lParam, out var hitResult))
                {
                    handled = true;
                    return hitResult;
                }
                break;
        }

        return IntPtr.Zero;
    }

    private bool HandleNcHitTest(IntPtr lParam, out IntPtr result)
    {
        result = IntPtr.Zero;
        if (WindowState == WindowState.Maximized)
            return false;

        int x = (short)(lParam.ToInt64() & 0xFFFF);
        int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);

        var point = PointFromScreen(new Point(x, y));
        const double resizeBorder = 6.0;

        bool onLeft = point.X <= resizeBorder;
        bool onRight = point.X >= ActualWidth - resizeBorder;
        bool onTop = point.Y <= resizeBorder;
        bool onBottom = point.Y >= ActualHeight - resizeBorder;

        if (onTop && onLeft) { result = (IntPtr)HTTOPLEFT; return true; }
        if (onTop && onRight) { result = (IntPtr)HTTOPRIGHT; return true; }
        if (onBottom && onLeft) { result = (IntPtr)HTBOTTOMLEFT; return true; }
        if (onBottom && onRight) { result = (IntPtr)HTBOTTOMRIGHT; return true; }
        if (onLeft) { result = (IntPtr)HTLEFT; return true; }
        if (onRight) { result = (IntPtr)HTRIGHT; return true; }
        if (onTop) { result = (IntPtr)HTTOP; return true; }
        if (onBottom) { result = (IntPtr)HTBOTTOM; return true; }

        return false;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                var rcWorkArea = monitorInfo.rcWork;
                var rcMonitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                mmi.ptMaxSize.x = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                mmi.ptMaxSize.y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                mmi.ptMinTrackSize.x = 1060;
                mmi.ptMinTrackSize.y = 560;
            }
        }
        Marshal.StructureToPtr(mmi, lParam, true);
    }

    #region Win32 Interop for Resizing & Maximizing

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_NCHITTEST = 0x0084;
    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    #endregion
}
