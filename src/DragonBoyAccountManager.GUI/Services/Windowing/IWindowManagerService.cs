using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DragonBoyAccountManager.GUI.Services.Windowing;

public interface IWindowManagerService
{
    IntPtr GetWindowHandle(Process proc);
    bool UpdateWindowTitle(Process proc, string newTitle);
    Task ApplyWindowStyleAsync(Process proc, Func<string> titleProvider, int width = 0, int height = 0, int maxAttempts = 50, CancellationToken cancellationToken = default);
    Task ApplyWindowStyleAsync(Process proc, string windowTitle, int width = 0, int height = 0, int maxAttempts = 50, CancellationToken cancellationToken = default);
    bool ResizeWindow(Process proc, int width, int height);
    bool PositionWindow(Process proc, int x, int y, int width = 0, int height = 0);
    void ArrangeWindows(IReadOnlyList<Process> processes, WindowArrangeOptions? options = null);
    bool BringToFront(Process proc);
}
