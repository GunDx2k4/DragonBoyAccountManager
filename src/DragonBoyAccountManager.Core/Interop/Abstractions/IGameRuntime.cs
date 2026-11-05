using DragonBoyAccountManager.Core.Interop.Memory;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Interop.Abstractions;

public interface IGameRuntime : IDisposable
{
    GameMemoryLayout Layout { get; }
    IMemoryAccessor Memory { get; }
    IntPtr ProcessHandle { get; }
    IntPtr ModuleBase { get; }
    bool Is64Bit { get; }

    T ReadStatic<T>(string className, int offset) where T : unmanaged;
    string ReadStaticString(string className, int offset);
    void WriteStatic<T>(string className, int offset, T value) where T : unmanaged;
    void WriteStaticString(string className, int offset, string value);
    IntPtr GetStaticFieldsPtr(string className);

    T ReadField<T>(IntPtr objectPtr, int offset) where T : unmanaged;
    IntPtr ReadFieldPtr(IntPtr objectPtr, int offset);
    string ReadStringField(IntPtr objectPtr, int offset);
    void WriteField<T>(IntPtr objectPtr, int offset, T value) where T : unmanaged;
    void WriteStringField(IntPtr objectPtr, int offset, string value);

    string ReadString(IntPtr stringPtr);
    IntPtr CreateString(string value);
    string? GetClassName(IntPtr objectPtr);
    string[] ReadStringArray(IntPtr arrayPtr);
    int[] ReadIntArray(IntPtr arrayPtr, int maxLength = 1_000_000);
    IntPtr[] ReadPointerArray(IntPtr arrayPtr);
    void WritePointerArrayElement(IntPtr arrayPtr, int index, IntPtr value);

    IntPtr CreateInstance(string className, params object?[]? args);

    IntPtr ResolveMethod(string className, string methodName, int paramCount = 0);
    IntPtr CallMethod(string className, string methodName, IntPtr thisObj, params object?[]? args);
}
