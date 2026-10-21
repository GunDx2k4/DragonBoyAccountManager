using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace DragonBoyAccountManager.GUI.Infrastructure.OS;

public sealed class WindowManagerService : IWindowManagerService
{
    public IntPtr GetWindowHandle(Process proc)
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

    private static User32.RECT GetWorkArea()
    {
        var rect = new User32.RECT();
        if (User32.SystemParametersInfoW(User32.SPI_GETWORKAREA, 0, ref rect, 0) && rect.Width > 200 && rect.Height > 200)
        {
            return rect;
        }
        return new User32.RECT { Left = 0, Top = 0, Right = 1920, Bottom = 1040 };
    }

    private static (int Width, int Height) GetWindowSize(IntPtr hWnd)
    {
        if (hWnd != IntPtr.Zero && User32.GetWindowRect(hWnd, out var r))
        {
            int w = r.Width;
            int h = r.Height;
            if (w > 100 && h > 100) return (w, h);
        }
        return (800, 600);
    }

    public bool UpdateWindowTitle(Process proc, string newTitle)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd == IntPtr.Zero) return false;

        return User32.SetWindowTextW(hWnd, newTitle);
    }

    public async Task ApplyWindowStyleAsync(Process proc, Func<string> titleProvider, int width = 0, int height = 0, int maxAttempts = 50)
    {
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
                        User32.SetWindowTextW(hWnd, title);
                    }

                    if (width > 0 && height > 0)
                    {
                        if (User32.IsIconic(hWnd))
                        {
                            User32.ShowWindow(hWnd, User32.SW_RESTORE);
                        }

                        var (curW, curH) = GetWindowSize(hWnd);
                        if (curW != width || curH != height)
                        {
                            uint flags = User32.SWP_NOZORDER | User32.SWP_NOMOVE | User32.SWP_SHOWWINDOW | User32.SWP_NOACTIVATE;
                            User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, width, height, flags);
                        }
                    }
                }
                catch { }
            }

            await Task.Delay(200);
        }
    }

    public Task ApplyWindowStyleAsync(Process proc, string windowTitle, int width = 0, int height = 0, int maxAttempts = 50)
        => ApplyWindowStyleAsync(proc, () => windowTitle, width, height, maxAttempts);

    public bool ResizeWindow(Process proc, int width, int height)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd == IntPtr.Zero || width <= 0 || height <= 0) return false;

        if (User32.IsIconic(hWnd))
        {
            User32.ShowWindow(hWnd, User32.SW_RESTORE);
        }

        uint flags = User32.SWP_NOZORDER | User32.SWP_NOMOVE | User32.SWP_SHOWWINDOW | User32.SWP_NOACTIVATE;
        return User32.SetWindowPos(hWnd, IntPtr.Zero, 0, 0, width, height, flags);
    }

    public bool PositionWindow(Process proc, int x, int y, int width = 0, int height = 0)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd == IntPtr.Zero) return false;

        if (User32.IsIconic(hWnd))
        {
            User32.ShowWindow(hWnd, User32.SW_RESTORE);
        }

        uint flags = User32.SWP_NOZORDER | User32.SWP_SHOWWINDOW | User32.SWP_NOACTIVATE;
        if (width <= 0 || height <= 0)
        {
            flags |= User32.SWP_NOSIZE;
        }

        return User32.SetWindowPos(hWnd, IntPtr.Zero, x, y, width, height, flags);
    }

    public void ArrangeWindows(IReadOnlyList<Process> processes, WindowArrangeOptions? options = null)
    {
        options ??= new WindowArrangeOptions();
        var validWindows = new List<IntPtr>();

        foreach (var proc in processes)
        {
            var hWnd = GetWindowHandle(proc);
            if (hWnd != IntPtr.Zero)
            {
                if (User32.IsIconic(hWnd))
                {
                    User32.ShowWindow(hWnd, User32.SW_RESTORE);
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

    private static void ArrangeTile(List<IntPtr> windows, User32.RECT workArea, WindowArrangeOptions options)
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

            User32.SetWindowPos(windows[i], IntPtr.Zero, x, y, 0, 0, User32.SWP_NOZORDER | User32.SWP_SHOWWINDOW | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
        }
    }

    private static void ArrangeCascade(List<IntPtr> windows, User32.RECT workArea, WindowArrangeOptions options)
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

            User32.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, User32.SWP_SHOWWINDOW | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
            User32.BringWindowToTop(hWnd);
        }
    }

    private static void ArrangeGrid(List<IntPtr> windows, User32.RECT workArea, WindowArrangeOptions options)
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

            User32.SetWindowPos(hWnd, IntPtr.Zero, x, y, 0, 0, User32.SWP_NOZORDER | User32.SWP_SHOWWINDOW | User32.SWP_NOSIZE | User32.SWP_NOACTIVATE);
        }
    }

    public bool BringToFront(Process proc)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd == IntPtr.Zero) return false;

        if (User32.IsIconic(hWnd))
        {
            User32.ShowWindow(hWnd, User32.SW_RESTORE);
        }

        User32.SetForegroundWindow(hWnd);
        User32.BringWindowToTop(hWnd);
        User32.SwitchToThisWindow(hWnd, true);
        return true;
    }

    public void SendKeyDown(Process proc, int vk)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd != IntPtr.Zero)
        {
            User32.PostMessageW(hWnd, User32.WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        }
    }

    public void SendKeyUp(Process proc, int vk)
    {
        var hWnd = GetWindowHandle(proc);
        if (hWnd != IntPtr.Zero)
        {
            User32.PostMessageW(hWnd, User32.WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
        }
    }
}
