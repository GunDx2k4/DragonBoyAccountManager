using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public abstract class GameObject(MonoReflector reflector)
{
    public MonoReflector Reflector { get; } = reflector;

    public abstract string ClassName { get; }
    public virtual IntPtr Pointer => IntPtr.Zero;
    public bool IsAlive => Pointer != IntPtr.Zero;

    protected T ReadStatic<T>(string fieldName) where T : unmanaged
        => Reflector.ReadStatic<T>(ClassName, fieldName);

    protected T ReadStatic<T>(string className, string fieldName) where T : unmanaged
        => Reflector.ReadStatic<T>(className, fieldName);

    protected string ReadStaticString(string fieldName)
        => Reflector.ReadStaticString(ClassName, fieldName);

    protected string ReadStaticString(string className, string fieldName)
        => Reflector.ReadStaticString(className, fieldName);

    protected void WriteStatic<T>(string fieldName, T value) where T : unmanaged
        => Reflector.WriteStatic(ClassName, fieldName, value);

    protected T ReadField<T>(string fieldName) where T : unmanaged
        => Pointer != IntPtr.Zero ? Reflector.ReadField<T>(ClassName, fieldName, Pointer) : default;

    protected string ReadFieldString(string fieldName)
        => Pointer != IntPtr.Zero ? Reflector.ReadStringField(ClassName, fieldName, Pointer) : string.Empty;

    protected IntPtr ReadFieldPtr(string fieldName)
        => Pointer != IntPtr.Zero ? Reflector.ReadFieldPtr(ClassName, fieldName, Pointer) : IntPtr.Zero;

    protected void WriteField<T>(string fieldName, T value) where T : unmanaged
    {
        if (Pointer != IntPtr.Zero)
            Reflector.WriteField(ClassName, fieldName, Pointer, value);
    }

    protected void WriteField<T>(string className, string fieldName, IntPtr ptr, T value) where T : unmanaged
    {
        if (ptr != IntPtr.Zero)
            Reflector.WriteField(className, fieldName, ptr, value);
    }

    protected void WriteFieldString(string fieldName, string value)
    {
        if (Pointer != IntPtr.Zero)
            Reflector.WriteStringField(ClassName, fieldName, Pointer, value);
    }

    protected void WriteFieldString(string className, string fieldName, IntPtr ptr, string value)
    {
        if (ptr != IntPtr.Zero)
            Reflector.WriteStringField(className, fieldName, ptr, value);
    }

    protected IntPtr CallMethod(string methodName, params object?[] args)
        => CallMethod(ClassName, methodName, args);

    protected IntPtr CallMethod(string className, string methodName, params object?[] args)
    {
        if (!IsAlive) return IntPtr.Zero;
        return Reflector.CallMethod(className, methodName, Pointer, args);
    }

    protected IntPtr CallStaticMethod(string methodName, params object?[] args)
        => CallStaticMethod(ClassName, methodName, args);

    protected IntPtr CallStaticMethod(string className, string methodName, params object?[] args)
        => Reflector.CallMethod(className, methodName, IntPtr.Zero, args);
}
