using System.Text;

namespace DragonBoyAccountManager.Core.Interop.Memory;

public interface IMemoryAccessor : IDisposable
{
    bool Is64Bit { get; }
    int PointerSize { get; }

    bool TryRead<T>(IntPtr address, out T value) where T : unmanaged;
    T Read<T>(IntPtr address) where T : unmanaged;

    bool TryWrite<T>(IntPtr address, T value) where T : unmanaged;
    void Write<T>(IntPtr address, T value) where T : unmanaged;

    byte[] ReadBytes(IntPtr address, int size);

    byte ReadByte(IntPtr address);
    ushort ReadUShort(IntPtr address);
    int ReadInt(IntPtr address);
    uint ReadUInt(IntPtr address);
    long ReadLong(IntPtr address);
    IntPtr ReadIntPtr(IntPtr address);

    string ReadString(IntPtr address, int length, Encoding encoding);
    string ReadUTF8String(IntPtr address, int length);
    string ReadUnicodeString(IntPtr address, int length);

    IntPtr Allocate(int size);
    bool Free(IntPtr address);
}
