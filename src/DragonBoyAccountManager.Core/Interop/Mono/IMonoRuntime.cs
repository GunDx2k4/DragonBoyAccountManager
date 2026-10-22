namespace DragonBoyAccountManager.Core.Interop.Mono;

public record MonoFieldInfo(IntPtr ClassPtr, IntPtr FieldPtr, int Offset);

public interface IMonoRuntime : IDisposable
{
    IntPtr RootDomain { get; }
    IMemoryAccessor Memory { get; }
    MonoFieldInfo? GetFieldInfo(string className, string fieldName, string ns = "\0", string assemblyName = "Assembly-CSharp");
    string? GetClassName(IntPtr monoObject);
    Task<bool> WaitForAssemblyAsync(string assemblyName = "Assembly-CSharp", int timeoutMs = 15000, CancellationToken cancellationToken = default);
    IntPtr OpenAssembly(string assemblyName = "Assembly-CSharp");
    IntPtr GetImage(string assemblyName = "Assembly-CSharp");
    IntPtr GetClass(string className, string ns = "\0", string assemblyName = "Assembly-CSharp");
    IntPtr GetVTable(IntPtr classPtr);
    IntPtr GetStaticFieldData(IntPtr vtable);

    T ReadStatic<T>(string className, string fieldName) where T : unmanaged;
    string ReadStaticString(string className, string fieldName);

    T ReadField<T>(string className, string fieldName, IntPtr objectPtr, string ns = "\0", string assemblyName = "Assembly-CSharp") where T : unmanaged;
    IntPtr ReadFieldPtr(string className, string fieldName, IntPtr objectPtr, string ns = "\0", string assemblyName = "Assembly-CSharp");
    string ReadStringField(string className, string fieldName, IntPtr objectPtr, string ns = "\0", string assemblyName = "Assembly-CSharp");

    string ReadMonoString(IntPtr monoStringPtr);
    string[] ReadMonoStringArray(IntPtr monoArrayPtr);
    int[] ReadMonoIntArray(IntPtr monoArrayPtr, int maxLength = 1_000_000);
    IntPtr[] ReadMonoPointerArray(IntPtr monoArrayPtr);
    void WriteMonoPointerArrayElement(IntPtr monoArrayPtr, int index, IntPtr value);

    void WriteField<T>(string className, string fieldName, IntPtr objectPtr, T value) where T : unmanaged;
    void WriteStringField(string className, string fieldName, IntPtr objectPtr, string value);
    void WriteStatic<T>(string className, string fieldName, T value) where T : unmanaged;
    void WriteStaticString(string className, string fieldName, string value);

    IntPtr ResolveMethod(string className, string methodName, int paramCount, string ns = "\0", string assemblyName = "Assembly-CSharp");
    IntPtr InvokeMethod(IntPtr methodPtr, IntPtr thisObj, params IntPtr[]? args);
    IntPtr CallMethod(string className, string methodName, IntPtr thisObj, params object?[]? args);
    IntPtr CallUnityMethod(string ns, string className, string methodName, IntPtr thisObj, params object?[]? args);
    IntPtr CreateMonoString(string value);
    IntPtr CreateInstance(string className, params object?[]? args);
}
