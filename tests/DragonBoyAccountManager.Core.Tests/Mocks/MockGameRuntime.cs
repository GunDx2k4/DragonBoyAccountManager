using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Memory;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Tests.Mocks;

public sealed class MockGameRuntime : IGameRuntime
{
    private readonly MockMemoryAccessor _memory;
    private readonly ConcurrentDictionary<string, IntPtr> _staticPointers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<IntPtr, string> _allocatedStrings = new();
    private bool _disposed;

    public GameMemoryLayout Layout { get; }
    public IMemoryAccessor Memory => _memory;
    public IntPtr ProcessHandle => (IntPtr)0x1000;
    public IntPtr ModuleBase => (IntPtr)0x20000000;
    public bool Is64Bit => true;

    public MockGameRuntime(GameMemoryLayout? layout = null, MockMemoryAccessor? memory = null)
    {
        _memory = memory ?? new MockMemoryAccessor();
        var schema = VersionOffsetProvider.Instance.GetDefaultSchema();
        Layout = layout ?? new GameMemoryLayout(schema);
    }

    public T ReadStatic<T>(string className, int offset) where T : unmanaged
    {
        IntPtr ptr = GetStaticFieldsPtr(className);
        return _memory.Read<T>(IntPtr.Add(ptr, offset));
    }

    public string ReadStaticString(string className, int offset)
    {
        IntPtr ptr = GetStaticFieldsPtr(className);
        IntPtr strPtr = _memory.Read<IntPtr>(IntPtr.Add(ptr, offset));
        return ReadString(strPtr);
    }

    public void WriteStatic<T>(string className, int offset, T value) where T : unmanaged
    {
        IntPtr ptr = GetStaticFieldsPtr(className);
        _memory.Write<T>(IntPtr.Add(ptr, offset), value);
    }

    public void WriteStaticString(string className, int offset, string value)
    {
        IntPtr ptr = GetStaticFieldsPtr(className);
        IntPtr strPtr = CreateString(value);
        _memory.Write<IntPtr>(IntPtr.Add(ptr, offset), strPtr);
    }

    public IntPtr GetStaticFieldsPtr(string className)
    {
        return _staticPointers.GetOrAdd(className, _ => _memory.Allocate(0x1000));
    }

    public T ReadField<T>(IntPtr objectPtr, int offset) where T : unmanaged
    {
        return _memory.Read<T>(IntPtr.Add(objectPtr, offset));
    }

    public IntPtr ReadFieldPtr(IntPtr objectPtr, int offset)
    {
        return _memory.ReadIntPtr(IntPtr.Add(objectPtr, offset));
    }

    public string ReadStringField(IntPtr objectPtr, int offset)
    {
        IntPtr strPtr = ReadFieldPtr(objectPtr, offset);
        return ReadString(strPtr);
    }

    public void WriteField<T>(IntPtr objectPtr, int offset, T value) where T : unmanaged
    {
        _memory.Write<T>(IntPtr.Add(objectPtr, offset), value);
    }

    public void WriteStringField(IntPtr objectPtr, int offset, string value)
    {
        IntPtr strPtr = CreateString(value);
        _memory.Write<IntPtr>(IntPtr.Add(objectPtr, offset), strPtr);
    }

    public string ReadString(IntPtr stringPtr)
    {
        if (stringPtr == IntPtr.Zero) return string.Empty;
        if (_allocatedStrings.TryGetValue(stringPtr, out var str)) return str;

        int length = _memory.ReadInt(IntPtr.Add(stringPtr, 16));
        if (length <= 0 || length > 100_000) return string.Empty;
        return _memory.ReadUnicodeString(IntPtr.Add(stringPtr, 20), length);
    }

    public IntPtr CreateString(string value)
    {
        if (string.IsNullOrEmpty(value)) return IntPtr.Zero;

        byte[] chars = Encoding.Unicode.GetBytes(value);
        int totalSize = 20 + chars.Length + 2;
        IntPtr ptr = _memory.Allocate(totalSize);

        _memory.Write<int>(IntPtr.Add(ptr, 16), value.Length);
        Marshal.Copy(chars, 0, IntPtr.Add(ptr, 20), chars.Length);
        _allocatedStrings[ptr] = value;
        return ptr;
    }

    public string? GetClassName(IntPtr objectPtr) => "MockObject";

    public string[] ReadStringArray(IntPtr arrayPtr)
    {
        if (arrayPtr == IntPtr.Zero) return [];
        int count = _memory.ReadInt(IntPtr.Add(arrayPtr, 24));
        if (count <= 0 || count > 10_000) return [];

        string[] result = new string[count];
        for (int i = 0; i < count; i++)
        {
            IntPtr itemPtr = _memory.ReadIntPtr(IntPtr.Add(arrayPtr, 32 + (i * 8)));
            result[i] = ReadString(itemPtr);
        }
        return result;
    }

    public int[] ReadIntArray(IntPtr arrayPtr, int maxLength = 1_000_000)
    {
        if (arrayPtr == IntPtr.Zero) return [];
        int count = _memory.ReadInt(IntPtr.Add(arrayPtr, 24));
        if (count <= 0) return [];
        count = Math.Min(count, maxLength);

        int[] result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = _memory.ReadInt(IntPtr.Add(arrayPtr, 32 + (i * 4)));
        }
        return result;
    }

    public IntPtr[] ReadPointerArray(IntPtr arrayPtr)
    {
        if (arrayPtr == IntPtr.Zero) return [];
        int count = _memory.ReadInt(IntPtr.Add(arrayPtr, 24));
        if (count <= 0 || count > 100_000) return [];

        IntPtr[] result = new IntPtr[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = _memory.ReadIntPtr(IntPtr.Add(arrayPtr, 32 + (i * 8)));
        }
        return result;
    }

    public void WritePointerArrayElement(IntPtr arrayPtr, int index, IntPtr value)
    {
        if (arrayPtr == IntPtr.Zero) return;
        _memory.Write<IntPtr>(IntPtr.Add(arrayPtr, 32 + (index * 8)), value);
    }

    public IntPtr CreateInstance(string className, params object?[]? args) => _memory.Allocate(0x200);
    public uint PinObject(IntPtr objectPtr) => 1;
    public void UnpinObject(uint gcHandle) { }

    public IntPtr ResolveMethod(string className, string methodName, int paramCount = 0) => (IntPtr)0x3000;
    public IntPtr CallMethod(string className, string methodName, IntPtr thisObj, params object?[]? args) => IntPtr.Zero;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memory.Dispose();
        _staticPointers.Clear();
        _allocatedStrings.Clear();
    }
}
