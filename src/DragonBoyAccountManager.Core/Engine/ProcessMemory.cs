using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

using DragonBoyAccountManager.Core.Engine.Win32;

namespace DragonBoyAccountManager.Core.Engine;

public class ProcessMemory(IntPtr handle) : IDisposable
{
    private readonly IntPtr _handle = handle;

    private readonly ConcurrentDictionary<IntPtr, int> _allocations = new();

    public bool Is64Bit { get; } = ProcessHelper.Is64BitProcess(handle);
    public int PointerSize => Is64Bit ? 8 : 4;

    public bool Free(IntPtr address)
    {
        if (address == IntPtr.Zero) return false;
        _allocations.TryRemove(address, out _);
        return Native.VirtualFreeEx(_handle, address, 0, MemoryFreeType.MEM_RELEASE);
    }

    public unsafe T Read<T>(IntPtr address) where T : unmanaged
    {
        T result = default;
        if (!Native.ReadProcessMemory(_handle, address, &result, sizeof(T), out int bytesRead) || bytesRead < sizeof(T))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to read process memory at 0x{address:X}, read {bytesRead}/{sizeof(T)}", new Win32Exception(error));
        }
        return result;
    }

    public byte[] ReadBytes(IntPtr address, int size)
    {
        byte[] bytes = new byte[size];
        if (!Native.ReadProcessMemory(_handle, address, bytes, size, out int bytesRead))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to read process memory at 0x{address:X}, read {bytesRead}/{size}", new Win32Exception(error));
        }

        if (bytesRead < size)
            Array.Resize(ref bytes, bytesRead);

        return bytes;
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
    public T ReadStruct<T>(IntPtr address) where T : unmanaged => Read<T>(address);
    public IntPtr ReadIntPtr(IntPtr address) => Is64Bit ? (IntPtr)ReadLong(address) : (IntPtr)ReadUInt(address);
    public string ReadString(IntPtr address, int length, Encoding encoding)
    {
        if (address == IntPtr.Zero || length <= 0) return string.Empty;
        byte[] buffer = ReadBytes(address, length);
        int nullIndex = Array.IndexOf(buffer, (byte)0);
        int count = nullIndex >= 0 ? nullIndex : buffer.Length;
        return encoding.GetString(buffer, 0, count);
    }
    public string ReadUTF8String(IntPtr address, int length) => ReadString(address, length, Encoding.UTF8);
    public string ReadUnicodeString(IntPtr address, int length)
    {
        if (address == IntPtr.Zero || length <= 0) return string.Empty;
        byte[] bytes = ReadBytes(address, length);
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

    public unsafe void Write<T>(IntPtr address, T value) where T : unmanaged
    {
        if (!Native.WriteProcessMemory(_handle, address, &value, sizeof(T)))
            throw new InvalidOperationException($"Failed to write process memory at 0x{address:X}", new Win32Exception(Marshal.GetLastWin32Error()));
    }
    private void Write(IntPtr addr, byte[] data)
    {
        if (!Native.WriteProcessMemory(_handle, addr, data, data.Length))
            throw new InvalidOperationException("Failed to write process memory", new Win32Exception(Marshal.GetLastWin32Error()));
    }

    public IntPtr Allocate(int size)
    {
        IntPtr addr = Native.VirtualAllocEx(_handle, IntPtr.Zero, size, AllocationType.MEM_COMMIT, MemoryProtection.PAGE_EXECUTE_READWRITE);
        if (addr == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate process memory", new Win32Exception(Marshal.GetLastWin32Error()));
        _allocations[addr] = size;
        return addr;
    }

    public IntPtr AllocateAndWrite(byte[] data)
    {
        IntPtr addr = Allocate(data.Length);
        Write(addr, data);
        return addr;
    }
    public IntPtr AllocateAndWrite(string? data)
    {
        if (string.IsNullOrEmpty(data))
            return AllocateAndWrite([0]);

        byte[] bytes = Encoding.UTF8.GetBytes(data.EndsWith('\0') ? data : data + "\0");
        return AllocateAndWrite(bytes);
    }
    public unsafe IntPtr AllocateAndWrite<T>(T value) where T : unmanaged
    {
        IntPtr addr = Allocate(sizeof(T));
        Write(addr, value);
        return addr;
    }
    public IntPtr AllocateAndWrite(IntPtr data) => Is64Bit ? AllocateAndWrite(data.ToInt64()) : AllocateAndWrite(unchecked((int)(long)data));
    public IntPtr AllocateAndWrite(IntPtr[] data)
    {
        byte[] bytes = new byte[data.Length * PointerSize];
        for (int i = 0; i < data.Length; i++)
        {
            byte[] ptrBytes = Is64Bit ? BitConverter.GetBytes(data[i].ToInt64()) : BitConverter.GetBytes(unchecked((int)(long)data[i]));
            Buffer.BlockCopy(ptrBytes, 0, bytes, i * PointerSize, PointerSize);
        }
        return AllocateAndWrite(bytes);
    }

    public void Dispose()
    {
        foreach (var address in _allocations.Keys.ToList())
        {
            try
            {
                Native.VirtualFreeEx(_handle, address, 0, MemoryFreeType.MEM_RELEASE);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
            }
        }
        _allocations.Clear();
        GC.SuppressFinalize(this);
    }
}
