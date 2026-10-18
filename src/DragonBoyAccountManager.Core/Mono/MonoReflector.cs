using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Common;

namespace DragonBoyAccountManager.Core.Mono;

public record MonoFieldInfo(IntPtr ClassPtr, IntPtr FieldPtr, int Offset);

public class MonoReflector(MonoContext context)
{
    private readonly MonoContext _context = context;
    public MonoContext Context => _context;

    private readonly ConcurrentDictionary<string, IntPtr> _assemblyCache = new();
    private readonly ConcurrentDictionary<string, IntPtr> _imageCache = new();
    private readonly ConcurrentDictionary<(string Assembly, string Ns, string ClassName), IntPtr> _classCache = new();
    private readonly ConcurrentDictionary<IntPtr, IntPtr> _vtableCache = new();
    private readonly ConcurrentDictionary<(string Assembly, string Ns, string ClassName, string FieldName), MonoFieldInfo?> _fieldCache = new();
    private readonly ConcurrentDictionary<(string Assembly, string Ns, string ClassName, string MethodName, int ParamCount), IntPtr> _methodCache = new();

    public string? GetClassName(IntPtr monoObject)
    {
        if (monoObject == IntPtr.Zero) return null;
        try
        {
            IntPtr classPtr = _context.Invoker.Call(
                _context.GetExport(Constants.MonoExports.ObjectGetClass),
                monoObject);
            if (classPtr == IntPtr.Zero) return null;

            IntPtr namePtr = _context.Invoker.Call(
                _context.GetExport(Constants.MonoExports.ClassGetName),
                classPtr);
            if (namePtr == IntPtr.Zero) return null;

            return _context.Memory.ReadUTF8String(namePtr, 128);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> WaitForAssemblyAsync(string assemblyName = "Assembly-CSharp", int timeoutMs = 15000, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs && !cancellationToken.IsCancellationRequested)
        {
            if (_context.Process.HasExited)
                return false;

            if (OpenAssembly(assemblyName) != IntPtr.Zero)
                return true;

            await Task.Delay(200, cancellationToken);
        }
        return false;
    }

    public IntPtr OpenAssembly(string assemblyName = "Assembly-CSharp")
    {
        if (_assemblyCache.TryGetValue(assemblyName, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr namePtr = _context.Memory.AllocateAndWrite(assemblyName);
        try
        {
            IntPtr asm = _context.Invoker.Call(
                _context.GetExport(Constants.MonoExports.DomainAssemblyOpen),
                _context.RootDomain, namePtr);

            if (asm != IntPtr.Zero)
            {
                _assemblyCache[assemblyName] = asm;
                return asm;
            }
        }
        finally
        {
            _context.Memory.Free(namePtr);
        }

        string alternateName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? assemblyName[..^4]
            : assemblyName + ".dll";

        if (!_assemblyCache.TryGetValue(alternateName, out var altCached) || altCached == IntPtr.Zero)
        {
            IntPtr altPtr = _context.Memory.AllocateAndWrite(alternateName);
            try
            {
                IntPtr asm = _context.Invoker.Call(
                    _context.GetExport(Constants.MonoExports.DomainAssemblyOpen),
                    _context.RootDomain, altPtr);

                if (asm != IntPtr.Zero)
                {
                    _assemblyCache[assemblyName] = asm;
                    _assemblyCache[alternateName] = asm;
                    return asm;
                }
            }
            finally
            {
                _context.Memory.Free(altPtr);
            }
        }
        else
        {
            _assemblyCache[assemblyName] = altCached;
            return altCached;
        }

        return IntPtr.Zero;
    }

    public IntPtr GetImage(string assemblyName = "Assembly-CSharp")
    {
        if (_imageCache.TryGetValue(assemblyName, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr assembly = OpenAssembly(assemblyName);
        if (assembly == IntPtr.Zero) return IntPtr.Zero;

        IntPtr image = _context.Invoker.Call(
            _context.GetExport(Constants.MonoExports.AssemblyGetImage),
            assembly);

        if (image != IntPtr.Zero)
        {
            _imageCache[assemblyName] = image;
        }
        return image;
    }

    public IntPtr GetClass(string className, string ns = Constants.NullTerminator, string assemblyName = "Assembly-CSharp")
    {
        var key = (assemblyName, ns, className);
        if (_classCache.TryGetValue(key, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr image = GetImage(key.assemblyName);
        if (image == IntPtr.Zero) return IntPtr.Zero;

        IntPtr nsPtr = _context.Memory.AllocateAndWrite(key.ns);
        IntPtr namePtr = _context.Memory.AllocateAndWrite(key.className);
        try
        {
            IntPtr classPtr = _context.Invoker.Call(
                _context.GetExport(Constants.MonoExports.ClassFromName),
                image, nsPtr, namePtr);

            if (classPtr != IntPtr.Zero)
            {
                _classCache[key] = classPtr;
            }
            return classPtr;
        }
        finally
        {
            _context.Memory.Free(nsPtr);
            _context.Memory.Free(namePtr);
        }
    }

    public IntPtr GetVTable(IntPtr classPtr)
    {
        if (classPtr == IntPtr.Zero) return IntPtr.Zero;
        if (_vtableCache.TryGetValue(classPtr, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr vtable = _context.Invoker.Call(
            _context.GetExport(Constants.MonoExports.ClassVTable),
            _context.RootDomain, classPtr);

        if (vtable != IntPtr.Zero)
        {
            _vtableCache[classPtr] = vtable;
        }
        return vtable;
    }

    public MonoFieldInfo? GetField(string className, string fieldName, string ns = Constants.NullTerminator, string assemblyName = "Assembly-CSharp")
    {
        var key = (assemblyName, ns, className, fieldName);
        if (_fieldCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        IntPtr classPtr = GetClass(className, ns, assemblyName);
        if (classPtr == IntPtr.Zero) return null;

        IntPtr fieldNamePtr = _context.Memory.AllocateAndWrite(fieldName);
        try
        {
            IntPtr currentClass = classPtr;
            while (currentClass != IntPtr.Zero)
            {
                IntPtr fieldPtr = _context.Invoker.Call(
                    _context.GetExport(Constants.MonoExports.ClassGetFieldFromName),
                    currentClass, fieldNamePtr);

                if (fieldPtr != IntPtr.Zero)
                {
                    int offset = (int)_context.Invoker.Call(
                        _context.GetExport(Constants.MonoExports.FieldGetOffset),
                        fieldPtr);

                    var info = new MonoFieldInfo(currentClass, fieldPtr, offset);
                    _fieldCache[key] = info;
                    return info;
                }

                currentClass = _context.Invoker.Call(
                    _context.GetExport(Constants.MonoExports.ClassGetParent),
                    currentClass);
            }

            return null;
        }
        finally
        {
            _context.Memory.Free(fieldNamePtr);
        }
    }

    public T ReadStatic<T>(string className, string fieldName) where T : unmanaged
    {
        var info = GetField(className, fieldName);
        if (info == null || info.FieldPtr == IntPtr.Zero) return default;

        IntPtr vtable = GetVTable(info.ClassPtr);
        if (vtable == IntPtr.Zero) return default;

        IntPtr buf = _context.Memory.Allocate(16);
        try
        {
            _context.Invoker.Call(
                _context.GetExport(Constants.MonoExports.FieldStaticGetValue),
                vtable, info.FieldPtr, buf);

            if (typeof(T) == typeof(IntPtr))
            {
                return (T)(object)_context.Memory.ReadIntPtr(buf);
            }

            return _context.Memory.Read<T>(buf);
        }
        finally
        {
            _context.Memory.Free(buf);
        }
    }

    public string ReadStaticString(string className, string fieldName)
    {
        IntPtr strPtr = ReadStatic<IntPtr>(className, fieldName);
        return strPtr != IntPtr.Zero ? ReadMonoString(strPtr) : string.Empty;
    }

    public T ReadField<T>(string className, string fieldName, IntPtr objectPtr, string ns = Constants.NullTerminator, string assemblyName = "Assembly-CSharp") where T : unmanaged
    {
        if (objectPtr == IntPtr.Zero) return default;
        var info = GetField(className, fieldName, ns, assemblyName);
        if (info == null) return default;
        if (typeof(T) == typeof(IntPtr))
        {
            return (T)(object)_context.Memory.ReadIntPtr(objectPtr + info.Offset);
        }
        return _context.Memory.ReadStruct<T>(objectPtr + info.Offset);
    }

    public IntPtr ReadFieldPtr(string className, string fieldName, IntPtr objectPtr, string ns = Constants.NullTerminator, string assemblyName = "Assembly-CSharp")
    {
        if (objectPtr == IntPtr.Zero) return IntPtr.Zero;
        var info = GetField(className, fieldName, ns, assemblyName);
        if (info == null) return IntPtr.Zero;
        return _context.Memory.ReadIntPtr(objectPtr + info.Offset);
    }

    public string ReadStringField(string className, string fieldName, IntPtr objectPtr, string ns = Constants.NullTerminator, string assemblyName = "Assembly-CSharp")
    {
        IntPtr strPtr = ReadFieldPtr(className, fieldName, objectPtr, ns, assemblyName);
        return strPtr != IntPtr.Zero ? ReadMonoString(strPtr) : string.Empty;
    }

    public List<IntPtr> ReadMyVector(IntPtr vectorPtr)
    {
        var result = new List<IntPtr>();
        if (vectorPtr == IntPtr.Zero) return result;

        try
        {
            IntPtr arrayListPtr = ReadFieldPtr("MyVector", "a", vectorPtr);
            if (arrayListPtr != IntPtr.Zero)
            {
                var sizeField = GetField("ArrayList", "_size", "System.Collections", "mscorlib")
                             ?? GetField("ArrayList", "size", "System.Collections", "mscorlib");
                var itemsField = GetField("ArrayList", "_items", "System.Collections", "mscorlib")
                              ?? GetField("ArrayList", "items", "System.Collections", "mscorlib");

                if (sizeField != null && itemsField != null)
                {
                    int count = _context.Memory.ReadInt(arrayListPtr + sizeField.Offset);
                    if (count > 0 && count <= 2000)
                    {
                        IntPtr itemsArrayPtr = _context.Memory.ReadIntPtr(arrayListPtr + itemsField.Offset);
                        if (itemsArrayPtr != IntPtr.Zero)
                        {
                            int ptrSize = _context.Memory.PointerSize;
                            int arrayLen = _context.Memory.ReadInt(itemsArrayPtr + ptrSize * 3);
                            int actualCount = Math.Min(count, Math.Max(0, arrayLen));
                            int elemOffset = ptrSize * 4;

                            for (int i = 0; i < actualCount; i++)
                            {
                                IntPtr elem = _context.Memory.ReadIntPtr(itemsArrayPtr + elemOffset + i * ptrSize);
                                if (elem != IntPtr.Zero)
                                {
                                    result.Add(elem);
                                }
                            }
                            return result;
                        }
                    }
                    else if (count == 0)
                    {
                        return result;
                    }
                }
            }

            IntPtr sizeMethod = ResolveMethod("MyVector", "size", 0);
            IntPtr elemMethod = ResolveMethod("MyVector", "elementAt", 1);
            if (sizeMethod != IntPtr.Zero && elemMethod != IntPtr.Zero)
            {
                IntPtr sizeBoxed = InvokeMethod(sizeMethod, vectorPtr);
                int count = unchecked((int)(long)sizeBoxed);
                if (count > 0 && count <= 500)
                {
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr elem = CallMethod("MyVector", "elementAt", vectorPtr, i);
                        if (elem != IntPtr.Zero)
                        {
                            result.Add(elem);
                        }
                    }
                }
            }
        }
        catch
        {
        }

        return result;
    }

    public string ReadMonoString(IntPtr monoStringPtr)
    {
        if (monoStringPtr == IntPtr.Zero) return string.Empty;

        int ptrSize = _context.Memory.PointerSize;
        int length = _context.Memory.ReadInt(monoStringPtr + ptrSize * 2);
        if (length <= 0 || length > 4096) return string.Empty;

        return _context.Memory.ReadUnicodeString(monoStringPtr + ptrSize * 2 + 4, length * 2);
    }

    public string[] ReadMonoStringArray(IntPtr monoArrayPtr)
    {
        if (monoArrayPtr == IntPtr.Zero) return [];

        try
        {
            int ptrSize = _context.Memory.PointerSize;
            int length = _context.Memory.ReadInt(monoArrayPtr + ptrSize * 3);
            if (length <= 0 || length > 100) return [];

            int elemOffset = ptrSize * 4;
            var list = new List<string>(length);
            for (int i = 0; i < length; i++)
            {
                IntPtr strPtr = _context.Memory.ReadIntPtr(monoArrayPtr + elemOffset + i * ptrSize);
                if (strPtr != IntPtr.Zero)
                {
                    string str = ReadMonoString(strPtr);
                    if (!string.IsNullOrEmpty(str))
                        list.Add(str);
                }
            }
            return [.. list];
        }
        catch
        {
            return [];
        }
    }

    public int[] ReadMonoIntArray(IntPtr monoArrayPtr, int maxElements = 200_000)
    {
        if (monoArrayPtr == IntPtr.Zero) return [];

        try
        {
            int ptrSize = _context.Memory.PointerSize;
            int length = _context.Memory.ReadInt(monoArrayPtr + ptrSize * 3);
            if (length <= 0 || length > maxElements) return [];

            int elemOffset = ptrSize * 4;
            byte[] rawBytes = _context.Memory.ReadBytes(monoArrayPtr + elemOffset, length * 4);
            if (rawBytes.Length < length * 4) return [];

            var arr = new int[length];
            Buffer.BlockCopy(rawBytes, 0, arr, 0, rawBytes.Length);
            return arr;
        }
        catch
        {
            return [];
        }
    }

    public short[] ReadMonoShortArray(IntPtr monoArrayPtr, int maxElements = 200_000)
    {
        if (monoArrayPtr == IntPtr.Zero) return [];

        try
        {
            int ptrSize = _context.Memory.PointerSize;
            int length = _context.Memory.ReadInt(monoArrayPtr + ptrSize * 3);
            if (length <= 0 || length > maxElements) return [];

            int elemOffset = ptrSize * 4;
            byte[] rawBytes = _context.Memory.ReadBytes(monoArrayPtr + elemOffset, length * 2);
            if (rawBytes.Length < length * 2) return [];

            var arr = new short[length];
            Buffer.BlockCopy(rawBytes, 0, arr, 0, rawBytes.Length);
            return arr;
        }
        catch
        {
            return [];
        }
    }

    public IntPtr[] ReadMonoPointerArray(IntPtr monoArrayPtr)
    {
        if (monoArrayPtr == IntPtr.Zero) return [];

        try
        {
            int ptrSize = _context.Memory.PointerSize;
            int length = _context.Memory.ReadInt(monoArrayPtr + ptrSize * 3);
            if (length <= 0 || length > 1000) return [];

            int elemOffset = ptrSize * 4;
            var arr = new IntPtr[length];
            for (int i = 0; i < length; i++)
            {
                arr[i] = _context.Memory.ReadIntPtr(monoArrayPtr + elemOffset + i * ptrSize);
            }
            return arr;
        }
        catch
        {
            return [];
        }
    }

    public void WriteStatic<T>(string className, string fieldName, T value) where T : unmanaged
    {
        var info = GetField(className, fieldName);
        if (info == null || info.FieldPtr == IntPtr.Zero) return;

        IntPtr vtable = GetVTable(info.ClassPtr);
        if (vtable == IntPtr.Zero) return;

        IntPtr valPtr = typeof(T) == typeof(IntPtr)
            ? _context.Memory.AllocateAndWrite((IntPtr)(object)value)
            : _context.Memory.AllocateAndWrite(value);
        try
        {
            _context.Invoker.Call(
                _context.GetExport(Constants.MonoExports.FieldStaticSetValue),
                vtable, info.FieldPtr, valPtr);
        }
        finally
        {
            _context.Memory.Free(valPtr);
        }
    }

    public void WriteField<T>(string className, string fieldName, IntPtr objectPtr, T value) where T : unmanaged
    {
        if (objectPtr == IntPtr.Zero) return;
        var info = GetField(className, fieldName);
        if (info == null) return;
        if (typeof(T) == typeof(IntPtr))
        {
            IntPtr ptrVal = (IntPtr)(object)value;
            if (_context.Memory.Is64Bit)
                _context.Memory.Write(objectPtr + info.Offset, ptrVal.ToInt64());
            else
                _context.Memory.Write(objectPtr + info.Offset, unchecked((int)(long)ptrVal));
            return;
        }
        _context.Memory.Write(objectPtr + info.Offset, value);
    }

    public void WriteStringField(string className, string fieldName, IntPtr objectPtr, string value)
    {
        if (objectPtr == IntPtr.Zero) return;
        IntPtr monoStr = CreateMonoString(value ?? string.Empty);
        if (monoStr != IntPtr.Zero)
        {
            WriteField(className, fieldName, objectPtr, monoStr);
        }
    }

    public IntPtr ResolveMethod(string className, string methodName, int paramCount, string ns = Constants.NullTerminator, string assemblyName = "Assembly-CSharp")
    {
        var key = (assemblyName, ns, className, methodName, paramCount);
        if (_methodCache.TryGetValue(key, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr classPtr = GetClass(className, ns, assemblyName);
        if (classPtr == IntPtr.Zero && (className == "Time" || className == "Screen" || ns == "UnityEngine"))
        {
            classPtr = GetClass(className, "UnityEngine", "UnityEngine");
            if (classPtr == IntPtr.Zero)
                classPtr = GetClass(className, "UnityEngine", "UnityEngine.dll");
            if (classPtr == IntPtr.Zero)
                classPtr = GetClass(className, "UnityEngine", "UnityEngine.CoreModule");
            if (classPtr == IntPtr.Zero)
                classPtr = GetClass(className, "UnityEngine", "UnityEngine.CoreModule.dll");
        }
        if (classPtr == IntPtr.Zero) return IntPtr.Zero;

        IntPtr namePtr = _context.Memory.AllocateAndWrite(methodName);
        try
        {
            IntPtr currentClass = classPtr;
            while (currentClass != IntPtr.Zero)
            {
                IntPtr methodPtr = _context.Invoker.Call(
                    _context.GetExport(Constants.MonoExports.ClassGetMethodFromName),
                    currentClass, namePtr, (IntPtr)paramCount);

                if (methodPtr != IntPtr.Zero)
                {
                    _methodCache[key] = methodPtr;
                    return methodPtr;
                }

                currentClass = _context.Invoker.Call(
                    _context.GetExport(Constants.MonoExports.ClassGetParent),
                    currentClass);
            }

            return IntPtr.Zero;
        }
        finally
        {
            _context.Memory.Free(namePtr);
        }
    }

    public IntPtr CallUnityMethod(string ns, string className, string methodName, IntPtr thisObj, params object?[]? args)
    {
        int paramCount = args?.Length ?? 0;
        IntPtr method = ResolveMethod(className, methodName, paramCount, ns, "UnityEngine");
        if (method == IntPtr.Zero)
            method = ResolveMethod(className, methodName, paramCount, ns, "UnityEngine.CoreModule");
        if (method == IntPtr.Zero) return IntPtr.Zero;

        if (paramCount == 0)
            return InvokeMethod(method, thisObj);

        var tempAllocations = new List<IntPtr>();
        var monoArgs = new IntPtr[paramCount];
        try
        {
            for (int i = 0; i < paramCount; i++)
            {
                monoArgs[i] = MarshalArgument(args![i], tempAllocations);
            }

            return InvokeMethod(method, thisObj, monoArgs);
        }
        finally
        {
            foreach (var ptr in tempAllocations)
                _context.Memory.Free(ptr);
        }
    }

    public IntPtr InvokeMethod(IntPtr methodPtr, IntPtr thisObj, params IntPtr[]? args)
    {
        if (methodPtr == IntPtr.Zero) return IntPtr.Zero;

        IntPtr paramsPtr = IntPtr.Zero;
        IntPtr excPtr = _context.Memory.AllocateAndWrite((long)0);
        try
        {
            if (args != null && args.Length > 0)
                paramsPtr = _context.Memory.AllocateAndWrite(args);

            IntPtr result = _context.Invoker.Call(
                _context.GetExport(Constants.MonoExports.RuntimeInvoke),
                methodPtr, thisObj, paramsPtr, excPtr);

            IntPtr exc = _context.Memory.ReadIntPtr(excPtr);
            if (exc != IntPtr.Zero)
            {
                return IntPtr.Zero;
            }

            return result;
        }
        finally
        {
            if (paramsPtr != IntPtr.Zero)
                _context.Memory.Free(paramsPtr);
            _context.Memory.Free(excPtr);
        }
    }

    public IntPtr CallMethod(string className, string methodName, IntPtr thisObj, params object?[]? args)
    {
        int paramCount = args?.Length ?? 0;
        IntPtr method = ResolveMethod(className, methodName, paramCount);
        if (method == IntPtr.Zero) return IntPtr.Zero;

        if (paramCount == 0)
            return InvokeMethod(method, thisObj);

        var tempAllocations = new List<IntPtr>();
        var monoArgs = new IntPtr[paramCount];
        try
        {
            for (int i = 0; i < paramCount; i++)
            {
                monoArgs[i] = MarshalArgument(args![i], tempAllocations);
            }

            return InvokeMethod(method, thisObj, monoArgs);
        }
        finally
        {
            foreach (var ptr in tempAllocations)
                _context.Memory.Free(ptr);
        }
    }

    public IntPtr CreateInstance(string className, params object?[]? args)
    {
        try
        {
            IntPtr classPtr = GetClass(className);
            if (classPtr == IntPtr.Zero) return IntPtr.Zero;

            IntPtr exportObjNew = _context.GetExport(Constants.MonoExports.ObjectNew);
            if (exportObjNew == IntPtr.Zero) return IntPtr.Zero;

            IntPtr monoObject = _context.Invoker.Call(exportObjNew, _context.RootDomain, classPtr);
            if (monoObject == IntPtr.Zero) return IntPtr.Zero;

            if (args != null && args.Length > 0)
            {
                CallMethod(className, ".ctor", monoObject, args);
            }
            return monoObject;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public IntPtr CreateMonoString(string value)
    {
        IntPtr strPtr = _context.Memory.AllocateAndWrite(value);
        try
        {
            return _context.Invoker.Call(
                _context.GetExport(Constants.MonoExports.StringNew),
                _context.RootDomain, strPtr);
        }
        finally
        {
            _context.Memory.Free(strPtr);
        }
    }

    private IntPtr MarshalArgument(object? arg, List<IntPtr> tempAllocations)
    {
        switch (arg)
        {
            case null:
                return IntPtr.Zero;

            case string s:
                return CreateMonoString(s);

            case IntPtr p:
                return p;

            case bool b:
            {
                IntPtr p = _context.Memory.AllocateAndWrite((byte)(b ? 1 : 0));
                tempAllocations.Add(p);
                return p;
            }

            case byte u8:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(u8);
                tempAllocations.Add(p);
                return p;
            }

            case sbyte s8:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(s8);
                tempAllocations.Add(p);
                return p;
            }

            case short s16:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(s16);
                tempAllocations.Add(p);
                return p;
            }

            case ushort u16:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(u16);
                tempAllocations.Add(p);
                return p;
            }

            case int i32:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(i32);
                tempAllocations.Add(p);
                return p;
            }

            case uint u32:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(u32);
                tempAllocations.Add(p);
                return p;
            }

            case long i64:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(i64);
                tempAllocations.Add(p);
                return p;
            }

            case ulong u64:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(u64);
                tempAllocations.Add(p);
                return p;
            }

            case float f32:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(f32);
                tempAllocations.Add(p);
                return p;
            }

            case double f64:
            {
                IntPtr p = _context.Memory.AllocateAndWrite(f64);
                tempAllocations.Add(p);
                return p;
            }

            default:
                throw new NotSupportedException($"Unsupported argument type for Mono marshaling: {arg.GetType().FullName}");
        }
    }
}
