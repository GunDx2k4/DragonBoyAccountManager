using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using DragonBoyAccountManager.Core.Interop.Native;

namespace DragonBoyAccountManager.Core.Interop.Memory;

public class ProcessMemory(IntPtr handle) : IMemoryAccessor
{
    private readonly IntPtr _handle = handle;
    private readonly ConcurrentDictionary<IntPtr, int> _allocations = new();

    public bool Is64Bit { get; } = ProcessHelper.Is64BitProcess(handle);
    public int PointerSize => Is64Bit ? 8 : 4;

    private bool IsValidAddress(IntPtr address)
    {
        if (address == IntPtr.Zero) return false;
        long addr = address.ToInt64();
        if (addr < 0x10000) return false;
        if (!Is64Bit && addr > 0x7FFFFFFF) return false;
        return true;
    }

    public unsafe bool TryRead<T>(IntPtr address, out T value) where T : unmanaged
    {
        value = default;
        if (!IsValidAddress(address)) return false;

        fixed (T* pValue = &value)
        {
            return NativeMethods.ReadProcessMemory(
                _handle,
                address,
                pValue,
                (nuint)sizeof(T),
                out nuint bytesRead) && bytesRead == (nuint)sizeof(T);
        }
    }

    public T Read<T>(IntPtr address) where T : unmanaged
    {
        TryRead(address, out T value);
        return value;
    }

    public unsafe bool TryWrite<T>(IntPtr address, T value) where T : unmanaged
    {
        if (!IsValidAddress(address)) return false;

        return NativeMethods.WriteProcessMemory(
            _handle,
            address,
            &value,
            (nuint)sizeof(T),
            out nuint bytesWritten) && bytesWritten == (nuint)sizeof(T);
    }

    public void Write<T>(IntPtr address, T value) where T : unmanaged
    {
        TryWrite(address, value);
    }

    public unsafe bool TryReadBytes(IntPtr address, Span<byte> destination)
    {
        if (!IsValidAddress(address) || destination.IsEmpty) return false;

        fixed (byte* pDest = destination)
        {
            return NativeMethods.ReadProcessMemory(
                _handle,
                address,
                pDest,
                (nuint)destination.Length,
                out nuint bytesRead) && bytesRead == (nuint)destination.Length;
        }
    }

    public unsafe byte[] ReadBytes(IntPtr address, int size)
    {
        if (!IsValidAddress(address) || size <= 0) return [];

        byte[] buffer = new byte[size];
        fixed (byte* pBuffer = buffer)
        {
            if (NativeMethods.ReadProcessMemory(_handle, address, pBuffer, (nuint)size, out nuint bytesRead))
            {
                if ((int)bytesRead < size)
                {
                    Array.Resize(ref buffer, (int)bytesRead);
                }
                return buffer;
            }
        }
        return [];
    }

    public byte ReadByte(IntPtr address) => Read<byte>(address);
    public short ReadShort(IntPtr address) => Read<short>(address);
    public ushort ReadUShort(IntPtr address) => Read<ushort>(address);
    public int ReadInt(IntPtr address) => Read<int>(address);
    public uint ReadUInt(IntPtr address) => Read<uint>(address);
    public long ReadLong(IntPtr address) => Read<long>(address);
    public ulong ReadULong(IntPtr address) => Read<ulong>(address);
    public float ReadFloat(IntPtr address) => Read<float>(address);
    public double ReadDouble(IntPtr address) => Read<double>(address);

    public IntPtr ReadIntPtr(IntPtr address)
    {
        return Is64Bit ? (IntPtr)ReadLong(address) : (IntPtr)ReadUInt(address);
    }

    public string ReadString(IntPtr address, int length, Encoding encoding)
    {
        if (!IsValidAddress(address) || length <= 0) return string.Empty;
        byte[] buffer = ReadBytes(address, length);
        if (buffer.Length == 0) return string.Empty;

        int nullIndex = Array.IndexOf(buffer, (byte)0);
        int count = nullIndex >= 0 ? nullIndex : buffer.Length;
        return encoding.GetString(buffer, 0, count);
    }

    public string ReadUTF8String(IntPtr address, int length) => ReadString(address, length, Encoding.UTF8);

    public string ReadUnicodeString(IntPtr address, int length)
    {
        if (!IsValidAddress(address) || length <= 0) return string.Empty;
        byte[] bytes = ReadBytes(address, length);
        if (bytes.Length == 0) return string.Empty;

        int count = bytes.Length;
        for (int i = 0; i + 1 < bytes.Length; i += 2)
        {
            if (bytes[i] == 0 && bytes[i + 1] == 0)
            {
                count = i;
                break;
            }
        }
        return Encoding.Unicode.GetString(bytes, 0, count);
    }

    public IntPtr Allocate(int size)
    {
        if (size <= 0) return IntPtr.Zero;

        IntPtr addr = NativeMethods.VirtualAllocEx(
            _handle,
            IntPtr.Zero,
            (nuint)size,
            AllocationType.Commit | AllocationType.Reserve,
            MemoryProtection.ExecuteReadWrite);

        if (addr != IntPtr.Zero)
        {
            _allocations[addr] = size;
        }
        return addr;
    }

    public bool Free(IntPtr address)
    {
        if (address == IntPtr.Zero) return false;
        _allocations.TryRemove(address, out _);
        return NativeMethods.VirtualFreeEx(_handle, address, 0, MemoryFreeType.Release);
    }

    public unsafe IntPtr AllocateAndWrite(byte[] data)
    {
        if (data == null || data.Length == 0) return IntPtr.Zero;

        IntPtr addr = Allocate(data.Length);
        if (addr == IntPtr.Zero) return IntPtr.Zero;

        fixed (byte* pData = data)
        {
            NativeMethods.WriteProcessMemory(_handle, addr, pData, (nuint)data.Length, out _);
        }
        return addr;
    }

    public IntPtr AllocateAndWrite(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return AllocateAndWrite([(byte)0]);

        byte[] bytes = Encoding.UTF8.GetBytes(data.EndsWith('\0') ? data : data + "\0");
        return AllocateAndWrite(bytes);
    }

    public unsafe IntPtr AllocateAndWrite<T>(T value) where T : unmanaged
    {
        IntPtr addr = Allocate(sizeof(T));
        if (addr != IntPtr.Zero)
        {
            Write(addr, value);
        }
        return addr;
    }

    public IntPtr AllocateAndWrite(IntPtr data)
    {
        return Is64Bit
            ? AllocateAndWrite(data.ToInt64())
            : AllocateAndWrite(unchecked((int)(long)data));
    }

    public IntPtr AllocateAndWrite(IntPtr[] data)
    {
        if (data == null || data.Length == 0) return IntPtr.Zero;

        int ptrSize = PointerSize;
        byte[] bytes = new byte[data.Length * ptrSize];
        for (int i = 0; i < data.Length; i++)
        {
            if (Is64Bit)
            {
                BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(i * 8, 8), data[i].ToInt64());
            }
            else
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), unchecked((int)(long)data[i]));
            }
        }
        return AllocateAndWrite(bytes);
    }

    public void Dispose()
    {
        foreach (var address in _allocations.Keys.ToList())
        {
            NativeMethods.VirtualFreeEx(_handle, address, 0, MemoryFreeType.Release);
        }
        _allocations.Clear();
        GC.SuppressFinalize(this);
    }
}
