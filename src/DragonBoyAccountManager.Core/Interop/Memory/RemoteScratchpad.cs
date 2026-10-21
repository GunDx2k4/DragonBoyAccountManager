using DragonBoyAccountManager.Core.Interop.Native;

namespace DragonBoyAccountManager.Core.Interop.Memory;

public sealed class RemoteScratchpad : IDisposable
{
    private readonly IntPtr _processHandle;
    private readonly IntPtr _baseAddress;
    private readonly int _totalSize;
    private readonly object _lock = new();

    public const int DefaultScratchpadSize = 64 * 1024;

    public IntPtr BaseAddress => _baseAddress;
    public IntPtr ReturnValueBuffer => _baseAddress;
    public IntPtr ArgumentsBuffer => _baseAddress + 0x0010;
    public IntPtr StringDataBuffer => _baseAddress + 0x0100;
    public IntPtr ShellcodeBuffer => _baseAddress + 0x1000;

    public RemoteScratchpad(IntPtr processHandle, int totalSize = DefaultScratchpadSize)
    {
        _processHandle = processHandle;
        _totalSize = totalSize;
        _baseAddress = NativeMethods.VirtualAllocEx(
            processHandle,
            IntPtr.Zero,
            (nuint)totalSize,
            AllocationType.Commit | AllocationType.Reserve,
            MemoryProtection.ExecuteReadWrite);

        if (_baseAddress == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to allocate remote scratchpad in target process.");
        }
    }

    public T WithLock<T>(Func<RemoteScratchpad, T> action)
    {
        lock (_lock)
        {
            return action(this);
        }
    }

    public void WithLock(Action<RemoteScratchpad> action)
    {
        lock (_lock)
        {
            action(this);
        }
    }

    public void Dispose()
    {
        if (_baseAddress != IntPtr.Zero)
        {
            NativeMethods.VirtualFreeEx(_processHandle, _baseAddress, 0, MemoryFreeType.Release);
        }
        GC.SuppressFinalize(this);
    }
}
