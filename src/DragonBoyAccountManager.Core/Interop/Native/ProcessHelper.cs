using System.Diagnostics;
using DragonBoyAccountManager.Core.Common;

namespace DragonBoyAccountManager.Core.Interop.Native;

public static class ProcessHelper
{
    public static bool Is64BitProcess(IntPtr handle)
    {
        if (!Environment.Is64BitOperatingSystem)
            return false;
        if (!NativeMethods.IsWow64Process(handle, out bool isWow64))
            return Environment.Is64BitProcess;
        return !isWow64;
    }

    public static bool IsProcessAlive(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return false;
        return NativeMethods.WaitForSingleObject(handle, 0) == WaitResult.Timeout;
    }

    public static unsafe IntPtr GetModuleBaseAddress(IntPtr handle, bool is64Bit, string moduleName)
    {
        int ptrSize = is64Bit ? Constants.PointerSize64 : Constants.PointerSize32;

        if (!NativeMethods.EnumProcessModulesEx(handle, null, 0, out uint bytesNeeded, ModuleFilter.All))
            return IntPtr.Zero;

        int moduleCount = (int)(bytesNeeded / (uint)ptrSize);
        var moduleHandles = new IntPtr[moduleCount];

        fixed (IntPtr* pModules = moduleHandles)
        {
            if (!NativeMethods.EnumProcessModulesEx(handle, pModules, bytesNeeded, out bytesNeeded, ModuleFilter.All))
                return IntPtr.Zero;
        }

        Span<char> pathBuffer = stackalloc char[Constants.MaxPathLength];

        for (int i = 0; i < moduleCount; i++)
        {
            fixed (char* pBuffer = pathBuffer)
            {
                uint length = NativeMethods.GetModuleFileNameEx(handle, moduleHandles[i], pBuffer, (uint)pathBuffer.Length);
                if (length > 0)
                {
                    string fullPath = pathBuffer[..(int)length].ToString();
                    string fileName = Path.GetFileName(fullPath);
                    if (fileName.Equals(moduleName, StringComparison.OrdinalIgnoreCase))
                    {
                        return moduleHandles[i];
                    }
                }
            }
        }
        return IntPtr.Zero;
    }

    public static unsafe IntPtr FindAnyMonoModule(IntPtr handle, bool is64Bit)
    {
        int ptrSize = is64Bit ? Constants.PointerSize64 : Constants.PointerSize32;

        if (!NativeMethods.EnumProcessModulesEx(handle, null, 0, out uint bytesNeeded, ModuleFilter.All))
            return IntPtr.Zero;

        int moduleCount = (int)(bytesNeeded / (uint)ptrSize);
        var moduleHandles = new IntPtr[moduleCount];

        fixed (IntPtr* pModules = moduleHandles)
        {
            if (!NativeMethods.EnumProcessModulesEx(handle, pModules, bytesNeeded, out bytesNeeded, ModuleFilter.All))
                return IntPtr.Zero;
        }

        Span<char> pathBuffer = stackalloc char[Constants.MaxPathLength];

        for (int i = 0; i < moduleCount; i++)
        {
            fixed (char* pBuffer = pathBuffer)
            {
                uint length = NativeMethods.GetModuleFileNameEx(handle, moduleHandles[i], pBuffer, (uint)pathBuffer.Length);
                if (length > 0)
                {
                    string fullPath = pathBuffer[..(int)length].ToString();
                    string fileName = Path.GetFileName(fullPath);
                    if (fileName.Contains("mono", StringComparison.OrdinalIgnoreCase))
                    {
                        return moduleHandles[i];
                    }
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
                IntPtr baseAddress = GetModuleBaseAddress(handle, is64Bit, moduleName);
                if (baseAddress != IntPtr.Zero)
                    return baseAddress;
            }

            IntPtr anyMono = FindAnyMonoModule(handle, is64Bit);
            if (anyMono != IntPtr.Zero)
                return anyMono;

            await Task.Delay(Constants.DefaultPollingIntervalMs, cancellationToken);
        }

        return IntPtr.Zero;
    }
}
