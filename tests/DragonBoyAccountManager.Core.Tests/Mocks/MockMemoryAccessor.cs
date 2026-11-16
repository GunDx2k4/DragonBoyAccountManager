using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using DragonBoyAccountManager.Core.Interop.Memory;

namespace DragonBoyAccountManager.Core.Tests.Mocks;

public sealed unsafe class MockMemoryAccessor : IMemoryAccessor
{
    private readonly HashSet<IntPtr> _allocations = [];
    private bool _disposed;

    public bool Is64Bit => Environment.Is64BitProcess;
    public int PointerSize => IntPtr.Size;

    public bool TryRead<T>(IntPtr address, out T value) where T : unmanaged
    {
        if (address == IntPtr.Zero)
        {
            value = default;
            return false;
        }

        try
        {
            value = Unsafe.Read<T>((void*)address);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public T Read<T>(IntPtr address) where T : unmanaged
    {
        if (address == IntPtr.Zero) throw new ArgumentNullException(nameof(address));
        return Unsafe.Read<T>((void*)address);
    }

    public bool TryWrite<T>(IntPtr address, T value) where T : unmanaged
    {
        if (address == IntPtr.Zero) return false;
        try
        {
            Unsafe.Write((void*)address, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Write<T>(IntPtr address, T value) where T : unmanaged
    {
        if (address == IntPtr.Zero) throw new ArgumentNullException(nameof(address));
        Unsafe.Write((void*)address, value);
    }

    public byte[] ReadBytes(IntPtr address, int size)
    {
        if (address == IntPtr.Zero || size <= 0) return [];
        byte[] buffer = new byte[size];
        Marshal.Copy(address, buffer, 0, size);
        return buffer;
    }

    public byte ReadByte(IntPtr address) => Read<byte>(address);
    public ushort ReadUShort(IntPtr address) => Read<ushort>(address);
    public int ReadInt(IntPtr address) => Read<int>(address);
    public uint ReadUInt(IntPtr address) => Read<uint>(address);
    public long ReadLong(IntPtr address) => Read<long>(address);
    public IntPtr ReadIntPtr(IntPtr address) => Read<IntPtr>(address);

    public string ReadString(IntPtr address, int length, Encoding encoding)
    {
        if (address == IntPtr.Zero || length <= 0) return string.Empty;
        byte[] bytes = ReadBytes(address, length);
        return encoding.GetString(bytes);
    }

    public string ReadUTF8String(IntPtr address, int length) => ReadString(address, length, Encoding.UTF8);
    public string ReadUnicodeString(IntPtr address, int length) => ReadString(address, length * 2, Encoding.Unicode);

    public IntPtr Allocate(int size)
    {
        IntPtr ptr = Marshal.AllocHGlobal(size);
        Unsafe.InitBlockUnaligned((void*)ptr, 0, (uint)size);
        _allocations.Add(ptr);
        return ptr;
    }

    public bool Free(IntPtr address)
    {
        if (_allocations.Remove(address))
        {
            Marshal.FreeHGlobal(address);
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (IntPtr ptr in _allocations)
        {
            Marshal.FreeHGlobal(ptr);
        }
        _allocations.Clear();
    }
}
