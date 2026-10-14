using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DragonBoyAccountManager.GUI.Helpers;

public enum WindowArrangeMode
{
    Grid,       // Lưới tự động (xuống hàng khi chạm mép màn hình)
    Tile,       // Chia đều khít màn hình (Auto Fit N tab)
    Cascade     // Bậc thang thông minh
}

public sealed class WindowArrangeOptions
{
    public WindowArrangeMode Mode { get; set; } = WindowArrangeMode.Grid;
    public int Columns { get; set; } = 0; // 0 = Tự động theo độ phân giải
    public int CustomWidth { get; set; } = 0; // 0 = Giữ nguyên / tự động
    public int CustomHeight { get; set; } = 0; // 0 = Giữ nguyên / tự động
    public int SpacingX { get; set; } = 0;
    public int SpacingY { get; set; } = 0;
}

public static class GameWindowHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Math.Max(0, Right - Left);
        public int Height => Math.Max(0, Bottom - Top);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    private const int GWL_STYLE = -16;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const int SW_RESTORE = 9;
    private const uint SPI_GETWORKAREA = 0x0030;

    public static IntPtr GetWindowHandle(Process proc)
    {
        try
        {
            if (proc.HasExited) return IntPtr.Zero;
            if (proc.MainWindowHandle != IntPtr.Zero) return proc.MainWindowHandle;
            proc.Refresh();
            return proc.MainWindowHandle;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public static RECT GetWorkArea()
    {
        var rect = new RECT();
        if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref rect, 0) && rect.Width > 200 && rect.Height > 200)
        {
            return rect;
        }
        return new RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
    }

    public static (int Width, int Height) GetWindowSize(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero && GetWindowRect(hWnd, out var r))
        {
            int w = r.Width;
            int h = r.Height;
            if (w > 100 && h > 100) return (w, h);
        }
        return (800, 600);
    }

    public static bool RemoveWindowButtons(IntPtr hWnd)
    {
        // Không can thiệp sửa style GWL_STYLE (WS_SYSMENU) của game vì sẽ khiến engine Unity 5.6 bị pause và văng ứng dụng
        return true;
    }

    public static bool UpdateWindowTitle(Process proc, string newTitle)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd == IntPtr.Zero) return false;

        return SetWindowText(hWnd, newTitle);
    }

    public static async Task ApplyWindowStyleAsync(Process proc, Func<string> titleProvider, int maxAttempts = 15, int requiredSuccessCount = 3)
    {
        int appliedCount = 0;
        for (int i = 0; i < maxAttempts; i++)
        {
            if (proc.HasExited) return;

            proc.Refresh();
            var hWnd = proc.MainWindowHandle;
            if (hWnd != IntPtr.Zero)
            {
                try
                {
                    string title = titleProvider();
                    if (!string.IsNullOrEmpty(title))
                    {
                        SetWindowText(hWnd, title);
                    }

                    appliedCount++;
                    if (appliedCount >= requiredSuccessCount)
                    {
                        return;
                    }
                }
                catch { }
            }

            await Task.Delay(250);
        }
    }

    public static Task ApplyWindowStyleAsync(Process proc, string windowTitle, int maxAttempts = 15, int requiredSuccessCount = 3)
        => ApplyWindowStyleAsync(proc, () => windowTitle, maxAttempts, requiredSuccessCount);

    public static bool PositionWindow(Process proc, int x, int y, int width = 0, int height = 0)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd == IntPtr.Zero) return false;

        if (IsIconic(hWnd))
        {
            ShowWindow(hWnd, SW_RESTORE);
        }

        uint flags = SWP_NOZORDER | SWP_SHOWWINDOW | SWP_NOACTIVATE;
        if (width <= 0 || height <= 0)
        {
            flags |= SWP_NOSIZE;
        }

        return SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, flags);
    }

    public static (int X, int Y) CalculateStaircasePosition(int slotIndex, int windowWidth, int windowHeight)
    {
        var workArea = GetWorkArea();
        int startX = workArea.Left + 25;
        int startY = workArea.Top + 25;
        int stepX = 35;
        int stepY = 30;

        return (startX + (slotIndex * stepX), startY + (slotIndex * stepY));
    }

    public static void ArrangeWindows(IReadOnlyList<Process> processes, WindowArrangeOptions? options = null)
    {
        options ??= new WindowArrangeOptions();
        var validWindows = new List<IntPtr>();

        foreach (var proc in processes)
        {
            var hWnd = GetWindowHandle(proc);
            if (hWnd != IntPtr.Zero)
            {
                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }
                validWindows.Add(hWnd);
            }
        }

        int count = validWindows.Count;
        if (count == 0) return;

        var workArea = GetWorkArea();

        switch (options.Mode)
        {
            case WindowArrangeMode.Tile:
                ArrangeTile(validWindows, workArea, options);
                break;

            case WindowArrangeMode.Cascade:
                ArrangeCascade(validWindows, workArea, options);
                break;

            case WindowArrangeMode.Grid:
            default:
                ArrangeGrid(validWindows, workArea, options);
                break;
        }
    }

    private static void ArrangeTile(List<IntPtr> windows, RECT workArea, WindowArrangeOptions options)
    {
        int count = windows.Count;
        int cols, rows;

        if (options.Columns > 0)
        {
            cols = options.Columns;
            rows = (int)Math.Ceiling((double)count / cols);
        }
        else
        {
            if (count == 1) { cols = 1; rows = 1; }
            else if (count == 2) { cols = 2; rows = 1; }
            else if (count == 3) { cols = 3; rows = 1; }
            else if (count <= 4) { cols = 2; rows = 2; }
            else if (count <= 6) { cols = 3; rows = 2; }
            else if (count <= 8) { cols = 4; rows = 2; }
            else if (count <= 9) { cols = 3; rows = 3; }
            else if (count <= 12) { cols = 4; rows = 3; }
            else if (count <= 16) { cols = 4; rows = 4; }
            else if (count <= 20) { cols = 5; rows = 4; }
            else if (count <= 25) { cols = 5; rows = 5; }
            else
            {
                cols = (int)Math.Ceiling(Math.Sqrt(count));
                rows = (int)Math.Ceiling((double)count / cols);
            }
        }

        cols = Math.Max(1, cols);
        rows = Math.Max(1, rows);

        int cellWidth = (workArea.Width - (cols - 1) * options.SpacingX) / cols;
        int cellHeight = (workArea.Height - (rows - 1) * options.SpacingY) / rows;

        for (int i = 0; i < count; i++)
        {
            int c = i % cols;
            int r = i / cols;

            int x = workArea.Left + (c * (cellWidth + options.SpacingX));
            int y = workArea.Top + (r * (cellHeight + options.SpacingY));

            SetWindowPos(windows[i], IntPtr.Zero, x, y, cellWidth, cellHeight, SWP_NOZORDER | SWP_SHOWWINDOW | SWP_NOACTIVATE);
        }
    }

    private static void ArrangeCascade(List<IntPtr> windows, RECT workArea, WindowArrangeOptions options)
    {
        int startX = workArea.Left + 25;
        int startY = workArea.Top + 25;
        int stepX = 35;
        int stepY = 30;

        int currentStartX = startX;
        int currentStartY = startY;

        for (int i = 0; i < windows.Count; i++)
        {
            var hWnd = windows[i];
            var (w, h) = GetWindowSize(hWnd);

            int x = currentStartX + (i * stepX);
            int y = currentStartY + (i * stepY);

            if (x + w > workArea.Right || y + h > workArea.Bottom)
            {
                currentStartX += 60;
                currentStartY = startY;
                x = currentStartX;
                y = currentStartY;
            }

            uint flags = SWP_SHOWWINDOW | SWP_NOSIZE | SWP_NOACTIVATE;
            SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, flags);
            BringWindowToTop(hWnd);
        }
    }

    private static void ArrangeGrid(List<IntPtr> windows, RECT workArea, WindowArrangeOptions options)
    {
        int count = windows.Count;

        var sample = GetWindowSize(windows[0]);
        int targetW = sample.Width;
        int targetH = sample.Height;

        if (targetW > workArea.Width) targetW = workArea.Width;
        if (targetH > workArea.Height) targetH = workArea.Height;

        int cols = options.Columns;
        if (cols <= 0)
        {
            cols = Math.Max(1, workArea.Width / Math.Max(1, targetW + options.SpacingX));
        }

        cols = Math.Max(1, cols);

        for (int i = 0; i < count; i++)
        {
            var hWnd = windows[i];
            int c = i % cols;
            int r = i / cols;

            int x = workArea.Left + (c * (targetW + options.SpacingX));
            int y = workArea.Top + (r * (targetH + options.SpacingY));

            if (y + targetH > workArea.Bottom && r > 0)
            {
                int maxRowsInScreen = Math.Max(1, workArea.Height / Math.Max(50, targetH));
                y = workArea.Top + ((r % maxRowsInScreen) * 35);
            }

            uint flags = SWP_NOZORDER | SWP_SHOWWINDOW | SWP_NOSIZE | SWP_NOACTIVATE;
            SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, flags);
        }
    }

    public static bool BringToFront(Process proc)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd == IntPtr.Zero) return false;

        if (IsIconic(hWnd))
        {
            ShowWindow(hWnd, SW_RESTORE);
        }

        SetForegroundWindow(hWnd);
        BringWindowToTop(hWnd);
        SwitchToThisWindow(hWnd, true);
        return true;
    }

    public static void SendKeyDown(Process proc, int vk)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd != IntPtr.Zero)
        {
            PostMessage(hWnd, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        }
    }

    public static void SendKeyUp(Process proc, int vk)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd != IntPtr.Zero)
        {
            PostMessage(hWnd, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
        }
    }
}
