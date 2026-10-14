using System.Diagnostics;
using System.Text;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Engine.Win32;

namespace DragonBoyAccountManager.Core.Engine;

public static class ProcessHelper
{
    public static bool Is64BitProcess(IntPtr handle)
    {
        if (!Environment.Is64BitOperatingSystem)
            return false;
        if (!Native.IsWow64Process(handle, out bool isWow64))
            return Environment.Is64BitProcess;
        return !isWow64;
    }

    public static bool IsProcessAlive(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;
        return Native.WaitForSingleObject(handle, 0) == WaitResult.WAIT_TIMEOUT;
    }

    public static IntPtr GetModuleBaseAddress(IntPtr handle, bool is64Bit, string moduleName)
    {
        int ptrSize = is64Bit ? Constants.PointerSize64 : Constants.PointerSize32;

        IntPtr[] moduleHandles = Array.Empty<IntPtr>();

        if (!Native.EnumProcessModulesEx(handle, moduleHandles, 0, out int bytesNeeded, ModuleFilter.LIST_MODULES_ALL))
            return IntPtr.Zero;

        int moduleCount = bytesNeeded / ptrSize;
        moduleHandles = new IntPtr[moduleCount];

        if (!Native.EnumProcessModulesEx(handle, moduleHandles, bytesNeeded, out bytesNeeded, ModuleFilter.LIST_MODULES_ALL))
            return IntPtr.Zero;

        var sb = new StringBuilder(Constants.MaxPathLength);

        for (int i = 0; i < moduleCount; i++)
        {
            sb.Clear();

            if (Native.GetModuleFileNameEx(handle, moduleHandles[i], sb, Constants.MaxPathLength) > 0)
            {
                string fileName = Path.GetFileName(sb.ToString());
                if (fileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                {
                    return moduleHandles[i];
                }
            }
        }
        return IntPtr.Zero;
    }

    public static IntPtr FindAnyMonoModule(IntPtr handle, bool is64Bit)
    {
        int ptrSize = is64Bit ? Constants.PointerSize64 : Constants.PointerSize32;
        IntPtr[] moduleHandles = Array.Empty<IntPtr>();

        if (!Native.EnumProcessModulesEx(handle, moduleHandles, 0, out int bytesNeeded, ModuleFilter.LIST_MODULES_ALL))
            return IntPtr.Zero;

        int moduleCount = bytesNeeded / ptrSize;
        moduleHandles = new IntPtr[moduleCount];

        if (!Native.EnumProcessModulesEx(handle, moduleHandles, bytesNeeded, out bytesNeeded, ModuleFilter.LIST_MODULES_ALL))
            return IntPtr.Zero;

        var sb = new StringBuilder(Constants.MaxPathLength);
        for (int i = 0; i < moduleCount; i++)
        {
            sb.Clear();
            if (Native.GetModuleFileNameEx(handle, moduleHandles[i], sb, Constants.MaxPathLength) > 0)
            {
                string fileName = Path.GetFileName(sb.ToString());
                if (fileName.IndexOf("mono", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return moduleHandles[i];
                }
            }
        }
        return IntPtr.Zero;
    }

    public static async Task<IntPtr> WaitForMonoModuleAsync(
        IntPtr handle,
        bool is64Bit,
        int timeoutMs = Constants.DefaultTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!cancellationToken.IsCancellationRequested && stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (!IsProcessAlive(handle))
                return IntPtr.Zero;

            foreach (string moduleName in Constants.MonoModuleNames)
            {
                IntPtr monoModule = GetModuleBaseAddress(handle, is64Bit, moduleName);
                if (monoModule != IntPtr.Zero)
                    return monoModule;
            }

            IntPtr anyMono = FindAnyMonoModule(handle, is64Bit);
            if (anyMono != IntPtr.Zero)
                return anyMono;

            await Task.Delay(Constants.DefaultPollingIntervalMs, cancellationToken);
        }

        return IntPtr.Zero;
    }
}
