using System.Runtime.InteropServices;

namespace DragonBoyAccountManager.Core.Interop.Native;

public static partial class NativeMethods
{
    private const string Kernel32 = "kernel32.dll";
    private const string PsApi = "psapi.dll";

    [LibraryImport(Kernel32, SetLastError = true)]
    public static partial IntPtr OpenProcess(
        ProcessAccessRights dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
        int processId);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(IntPtr handle);

    [LibraryImport(Kernel32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWow64Process(
        IntPtr hProcess,
        [MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

    [LibraryImport(Kernel32, SetLastError = true)]
    public static unsafe partial IntPtr VirtualAllocEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        nuint dwSize,
        AllocationType flAllocationType,
        MemoryProtection flProtect);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool VirtualFreeEx(
        IntPtr hProcess,
        IntPtr lpAddress,
        nuint dwSize,
        MemoryFreeType dwFreeType);

    [LibraryImport(Kernel32, SetLastError = true)]
    public static partial IntPtr CreateRemoteThread(
        IntPtr hProcess,
        IntPtr lpThreadAttributes,
        nuint dwStackSize,
        IntPtr lpStartAddress,
        IntPtr lpParameter,
        ThreadCreationFlags dwCreationFlags,
        out int lpThreadId);

    [LibraryImport(Kernel32, SetLastError = true)]
    public static partial WaitResult WaitForSingleObject(
        IntPtr hHandle,
        uint dwMilliseconds);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool ReadProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        void* lpBuffer,
        nuint nSize,
        out nuint lpNumberOfBytesRead);

    [LibraryImport(Kernel32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool WriteProcessMemory(
        IntPtr hProcess,
        IntPtr lpBaseAddress,
        void* lpBuffer,
        nuint nSize,
        out nuint lpNumberOfBytesWritten);

    [LibraryImport(PsApi, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static unsafe partial bool EnumProcessModulesEx(
        IntPtr hProcess,
        IntPtr* lphModule,
        uint cb,
        out uint lpcbNeeded,
        ModuleFilter dwFilterFlag);

    [LibraryImport(PsApi, EntryPoint = "GetModuleFileNameExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static unsafe partial uint GetModuleFileNameEx(
        IntPtr hProcess,
        IntPtr hModule,
        char* lpBaseName,
        uint nSize);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindow(IntPtr hWnd);
}
