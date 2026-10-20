using System.Text;

namespace DragonBoyAccountManager.Core.Domain.Contracts;

public interface IMemoryAccessor : IDisposable
{
    bool Is64Bit { get; }
    int PointerSize { get; }

    bool Free(IntPtr address);

    T Read<T>(IntPtr address) where T : unmanaged;
    byte[] ReadBytes(IntPtr address, int size);
    byte ReadByte(IntPtr address);
    short ReadShort(IntPtr address);
    ushort ReadUShort(IntPtr address);
    int ReadInt(IntPtr address);
    uint ReadUInt(IntPtr address);
    long ReadLong(IntPtr address);
    ulong ReadULong(IntPtr address);
    float ReadFloat(IntPtr address);
    double ReadDouble(IntPtr address);
    T ReadStruct<T>(IntPtr address) where T : unmanaged;
    IntPtr ReadIntPtr(IntPtr address);
    string ReadString(IntPtr address, int length, Encoding encoding);
    string ReadUTF8String(IntPtr address, int length);
    string ReadUnicodeString(IntPtr address, int length);

    void Write<T>(IntPtr address, T value) where T : unmanaged;

    IntPtr Allocate(int size);
    IntPtr AllocateAndWrite(byte[] data);
    IntPtr AllocateAndWrite(string? data);
    IntPtr AllocateAndWrite<T>(T value) where T : unmanaged;
    IntPtr AllocateAndWrite(IntPtr data);
    IntPtr AllocateAndWrite(IntPtr[] data);
}
