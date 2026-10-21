using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using DragonBoyAccountManager.GUI.ViewModels;

namespace DragonBoyAccountManager.GUI.Views;

public partial class MainWindow : Window
{
    private readonly IHotkeyRoutingService _hotkeyRoutingService;

    public MainWindow(MainViewModel viewModel, IHotkeyRoutingService hotkeyRoutingService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _hotkeyRoutingService = hotkeyRoutingService;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_hotkeyRoutingService.HandlePreviewKeyDown(e, this))
        {
            e.Handled = true;
        }
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_hotkeyRoutingService.HandlePreviewKeyUp(e))
        {
            e.Handled = true;
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        _hotkeyRoutingService.ReleaseAllHeldKeys();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case User32.WM_GETMINMAXINFO:
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
                break;

            case User32.WM_NCHITTEST:
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

        if (onTop && onLeft) { result = (IntPtr)User32.HTTOPLEFT; return true; }
        if (onTop && onRight) { result = (IntPtr)User32.HTTOPRIGHT; return true; }
        if (onBottom && onLeft) { result = (IntPtr)User32.HTBOTTOMLEFT; return true; }
        if (onBottom && onRight) { result = (IntPtr)User32.HTBOTTOMRIGHT; return true; }
        if (onLeft) { result = (IntPtr)User32.HTLEFT; return true; }
        if (onRight) { result = (IntPtr)User32.HTRIGHT; return true; }
        if (onTop) { result = (IntPtr)User32.HTTOP; return true; }
        if (onBottom) { result = (IntPtr)User32.HTBOTTOM; return true; }

        return false;
    }

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<User32.MINMAXINFO>(lParam);
        var monitor = User32.MonitorFromWindow(hwnd, User32.MONITOR_DEFAULTTONEAREST);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new User32.MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(User32.MONITORINFO));
            if (User32.GetMonitorInfoW(monitor, ref monitorInfo))
            {
                var rcWorkArea = monitorInfo.rcWork;
                var rcMonitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                mmi.ptMinTrackSize.X = 1060;
                mmi.ptMinTrackSize.Y = 600;
            }
        }
        Marshal.StructureToPtr(mmi, lParam, true);
    }
}
