using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Memory;
using DragonBoyAccountManager.Core.Interop.Native;

namespace DragonBoyAccountManager.Core.Interop.Mono;

public class MonoRuntime : IMonoRuntime
{
    private readonly IntPtr _processHandle;
    private readonly IMemoryAccessor _memory;
    private readonly IntPtr _monoModule;
    private readonly RemoteScratchpad _scratchpad;
    private readonly MonoRemoteInvoker _invoker;
    private readonly Dictionary<string, IntPtr> _exports = new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, IntPtr> _assemblyCache = new();
    private readonly ConcurrentDictionary<string, IntPtr> _imageCache = new();
    private readonly ConcurrentDictionary<(string Assembly, string Ns, string ClassName), IntPtr> _classCache = new();
    private readonly ConcurrentDictionary<IntPtr, IntPtr> _vtableCache = new();
    private readonly ConcurrentDictionary<(string Assembly, string Ns, string ClassName, string FieldName), MonoFieldInfo?> _fieldCache = new();
    private readonly ConcurrentDictionary<(string Assembly, string Ns, string ClassName, string MethodName, int ParamCount), IntPtr> _methodCache = new();
    private readonly ConcurrentDictionary<IntPtr, IntPtr> _staticDataCache = new();

    public IntPtr RootDomain => _invoker.RootDomain;
    public MonoRemoteInvoker Invoker => _invoker;
    public IMemoryAccessor Memory => _memory;

    private static readonly HashSet<string> DesiredExports = typeof(Constants.MonoExports)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetValue(null)!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private MonoRuntime(IntPtr processHandle, IMemoryAccessor memory, IntPtr monoModule, RemoteScratchpad scratchpad)
    {
        _processHandle = processHandle;
        _memory = memory;
        _monoModule = monoModule;
        _scratchpad = scratchpad;
        _invoker = new MonoRemoteInvoker(processHandle, memory, scratchpad);
    }

    public static async Task<MonoRuntime?> AttachAsync(Process? process, CancellationToken cancellationToken = default)
    {
        if (process == null || process.HasExited)
            return null;

        var handle = NativeMethods.OpenProcess(ProcessAccessRights.AllAccess, false, process.Id);
        if (handle == IntPtr.Zero)
            return null;

        var memory = new ProcessMemory(handle);
        RemoteScratchpad? scratchpad = null;
        try
        {
            var monoModule = await ProcessHelper.WaitForMonoModuleAsync(handle, memory.Is64Bit, cancellationToken: cancellationToken);
            if (monoModule == IntPtr.Zero)
            {
                memory.Dispose();
                NativeMethods.CloseHandle(handle);
                return null;
            }

            await Task.Delay(500, cancellationToken);

            scratchpad = new RemoteScratchpad(handle);
            var runtime = new MonoRuntime(handle, memory, monoModule, scratchpad);
            runtime.LoadExports();

            if (!await runtime.InitializeDomainAsync(cancellationToken))
            {
                runtime.Dispose();
                return null;
            }

            return runtime;
        }
        catch
        {
            scratchpad?.Dispose();
            memory.Dispose();
            NativeMethods.CloseHandle(handle);
            throw;
        }
    }

    private void LoadExports()
    {
        int e_lfanew = _memory.ReadInt(_monoModule + 0x3C);
        IntPtr ntHeaders = _monoModule + e_lfanew;
        IntPtr optionalHeader = ntHeaders + 0x18;

        IntPtr exportDataDir = optionalHeader + (_memory.Is64Bit ? 0x70 : 0x60);
        IntPtr exportRva = _memory.ReadInt(exportDataDir);
        uint exportSize = _memory.ReadUInt(exportDataDir + 0x04);

        if (exportRva == 0) return;

        IntPtr exportDir = _monoModule + (int)exportRva;
        int count = _memory.ReadInt(exportDir + 0x18);
        IntPtr functions = _monoModule + _memory.ReadInt(exportDir + 0x1C);
        IntPtr names = _monoModule + _memory.ReadInt(exportDir + 0x20);
        IntPtr ordinals = _monoModule + _memory.ReadInt(exportDir + 0x24);

        for (int i = 0; i < count; i++)
        {
            int nameRva = _memory.ReadInt(names + i * 4);
            string funcName = _memory.ReadString(_monoModule + nameRva, Constants.DefaultStringLength, Encoding.ASCII);

            if (!DesiredExports.Contains(funcName))
                continue;

            ushort ordinal = _memory.ReadUShort(ordinals + i * 2);
            uint funcRva = _memory.ReadUInt(functions + ordinal * 4);

            if (funcRva >= exportRva && funcRva < exportRva + exportSize)
                continue;

            IntPtr funcAddress = _monoModule + (int)funcRva;
            if (funcAddress != IntPtr.Zero)
                _exports[funcName] = funcAddress;

            if (_exports.Count >= DesiredExports.Count)
                break;
        }
    }

    private async Task<bool> InitializeDomainAsync(CancellationToken cancellationToken)
    {
        IntPtr getDomainAddr = GetExport(Constants.MonoExports.RootDomain);
        IntPtr threadAttachAddr = GetExport(Constants.MonoExports.ThreadAttach);
        IntPtr threadDetachAddr = GetExport(Constants.MonoExports.ThreadDetach);

        if (getDomainAddr == IntPtr.Zero || threadAttachAddr == IntPtr.Zero)
            return false;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 15000 && !cancellationToken.IsCancellationRequested)
        {
            if (!ProcessHelper.IsProcessAlive(_processHandle))
                return false;

            IntPtr domain = _invoker.Call(getDomainAddr);
            if (domain != IntPtr.Zero)
            {
                await Task.Delay(200, cancellationToken);
                _invoker.SetDomain(domain, threadAttachAddr, threadDetachAddr);
                return true;
            }
            await Task.Delay(100, cancellationToken);
        }

        return false;
    }

    public IntPtr GetExport(string name)
    {
        return _exports.TryGetValue(name, out var address) ? address : IntPtr.Zero;
    }

    public string? GetClassName(IntPtr monoObject)
    {
        if (monoObject == IntPtr.Zero) return null;
        try
        {
            IntPtr classPtr = _invoker.Call(GetExport(Constants.MonoExports.ObjectGetClass), monoObject);
            if (classPtr == IntPtr.Zero) return null;

            IntPtr namePtr = _invoker.Call(GetExport(Constants.MonoExports.ClassGetName), classPtr);
            if (namePtr == IntPtr.Zero) return null;

            return _memory.ReadUTF8String(namePtr, 128);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> WaitForAssemblyAsync(string assemblyName = "Assembly-CSharp", int timeoutMs = 15000, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs && !cancellationToken.IsCancellationRequested)
        {
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

        IntPtr namePtr = _memory.AllocateAndWrite(assemblyName);
        try
        {
            IntPtr asm = _invoker.Call(GetExport(Constants.MonoExports.DomainAssemblyOpen), RootDomain, namePtr);
            if (asm != IntPtr.Zero)
            {
                _assemblyCache[assemblyName] = asm;
                return asm;
            }
        }
        finally
        {
            _memory.Free(namePtr);
        }

        string altName = assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ? assemblyName[..^4]
            : assemblyName + ".dll";

        if (!_assemblyCache.TryGetValue(altName, out var altCached) || altCached == IntPtr.Zero)
        {
            IntPtr altPtr = _memory.AllocateAndWrite(altName);
            try
            {
                IntPtr asm = _invoker.Call(GetExport(Constants.MonoExports.DomainAssemblyOpen), RootDomain, altPtr);
                if (asm != IntPtr.Zero)
                {
                    _assemblyCache[assemblyName] = asm;
                    _assemblyCache[altName] = asm;
                    return asm;
                }
            }
            finally
            {
                _memory.Free(altPtr);
            }
        }
        return altCached;
    }

    public IntPtr GetImage(string assemblyName = "Assembly-CSharp")
    {
        if (_imageCache.TryGetValue(assemblyName, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr assembly = OpenAssembly(assemblyName);
        if (assembly == IntPtr.Zero) return IntPtr.Zero;

        IntPtr image = _invoker.Call(GetExport(Constants.MonoExports.AssemblyGetImage), assembly);
        if (image != IntPtr.Zero)
        {
            _imageCache[assemblyName] = image;
        }
        return image;
    }

    public IntPtr GetClass(string className, string ns = "\0", string assemblyName = "Assembly-CSharp")
    {
        var key = (assemblyName, ns, className);
        if (_classCache.TryGetValue(key, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr image = GetImage(assemblyName);
        if (image == IntPtr.Zero) return IntPtr.Zero;

        IntPtr namePtr = _memory.AllocateAndWrite(className);
        IntPtr nsPtr = _memory.AllocateAndWrite(ns);
        try
        {
            IntPtr classPtr = _invoker.Call(GetExport(Constants.MonoExports.ClassFromName), image, nsPtr, namePtr);
            if (classPtr != IntPtr.Zero)
            {
                _classCache[key] = classPtr;
            }
            return classPtr;
        }
        finally
        {
            _memory.Free(namePtr);
            _memory.Free(nsPtr);
        }
    }

    public IntPtr GetVTable(IntPtr classPtr)
    {
        if (classPtr == IntPtr.Zero) return IntPtr.Zero;
        if (_vtableCache.TryGetValue(classPtr, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr vtable = _invoker.Call(GetExport(Constants.MonoExports.ClassVTable), RootDomain, classPtr);
        if (vtable != IntPtr.Zero)
        {
            _vtableCache[classPtr] = vtable;
        }
        return vtable;
    }

    public IntPtr GetStaticFieldData(IntPtr vtable)
    {
        if (vtable == IntPtr.Zero) return IntPtr.Zero;
        if (_staticDataCache.TryGetValue(vtable, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr export = GetExport(Constants.MonoExports.VTableGetStaticFieldData);
        if (export != IntPtr.Zero)
        {
            IntPtr dataPtr = _invoker.Call(export, vtable);
            _staticDataCache[vtable] = dataPtr;
            return dataPtr;
        }

        IntPtr fallback = _memory.ReadIntPtr(vtable + (_memory.Is64Bit ? 0x28 : 0x14));
        _staticDataCache[vtable] = fallback;
        return fallback;
    }

    public MonoFieldInfo? GetFieldInfo(string className, string fieldName, string ns = "\0", string assemblyName = "Assembly-CSharp")
    {
        var key = (assemblyName, ns, className, fieldName);
        if (_fieldCache.TryGetValue(key, out var cached))
            return cached;

        IntPtr classPtr = GetClass(className, ns, assemblyName);
        if (classPtr == IntPtr.Zero) return null;

        IntPtr fieldNamePtr = _memory.AllocateAndWrite(fieldName);
        try
        {
            IntPtr currentClass = classPtr;
            while (currentClass != IntPtr.Zero)
            {
                IntPtr fieldPtr = _invoker.Call(GetExport(Constants.MonoExports.ClassGetFieldFromName), currentClass, fieldNamePtr);
                if (fieldPtr != IntPtr.Zero)
                {
                    int offset = (int)_invoker.Call(GetExport(Constants.MonoExports.FieldGetOffset), fieldPtr);
                    var info = new MonoFieldInfo(currentClass, fieldPtr, offset);
                    _fieldCache[key] = info;
                    return info;
                }

                currentClass = _invoker.Call(GetExport(Constants.MonoExports.ClassGetParent), currentClass);
            }
        }
        finally
        {
            _memory.Free(fieldNamePtr);
        }

        _fieldCache[key] = null;
        return null;
    }

    public unsafe T ReadStatic<T>(string className, string fieldName) where T : unmanaged
    {
        var info = GetFieldInfo(className, fieldName);
        if (info == null) return default;

        IntPtr vtable = GetVTable(info.ClassPtr);
        if (vtable == IntPtr.Zero) return default;

        IntPtr staticData = GetStaticFieldData(vtable);
        if (staticData != IntPtr.Zero)
        {
            if (typeof(T) == typeof(IntPtr))
            {
                return (T)(object)_memory.ReadIntPtr(staticData + info.Offset);
            }
            return _memory.Read<T>(staticData + info.Offset);
        }

        IntPtr buf = _memory.Allocate(_memory.PointerSize > sizeof(T) ? _memory.PointerSize : sizeof(T));
        try
        {
            _invoker.Call(GetExport(Constants.MonoExports.FieldStaticGetValue), vtable, info.FieldPtr, buf);
            if (typeof(T) == typeof(IntPtr))
            {
                return (T)(object)_memory.ReadIntPtr(buf);
            }
            return _memory.Read<T>(buf);
        }
        finally
        {
            _memory.Free(buf);
        }
    }

    public string ReadStaticString(string className, string fieldName)
    {
        IntPtr monoStringPtr = ReadStatic<IntPtr>(className, fieldName);
        return ReadMonoString(monoStringPtr);
    }

    public T ReadField<T>(string className, string fieldName, IntPtr objectPtr, string ns = "\0", string assemblyName = "Assembly-CSharp") where T : unmanaged
    {
        if (objectPtr == IntPtr.Zero) return default;
        var info = GetFieldInfo(className, fieldName, ns, assemblyName);
        if (info == null) return default;
        if (typeof(T) == typeof(IntPtr))
        {
            return (T)(object)_memory.ReadIntPtr(objectPtr + info.Offset);
        }
        return _memory.Read<T>(objectPtr + info.Offset);
    }

    public IntPtr ReadFieldPtr(string className, string fieldName, IntPtr objectPtr, string ns = "\0", string assemblyName = "Assembly-CSharp")
    {
        if (objectPtr == IntPtr.Zero) return IntPtr.Zero;
        var info = GetFieldInfo(className, fieldName, ns, assemblyName);
        return info == null ? IntPtr.Zero : _memory.ReadIntPtr(objectPtr + info.Offset);
    }

    public string ReadStringField(string className, string fieldName, IntPtr objectPtr, string ns = "\0", string assemblyName = "Assembly-CSharp")
    {
        IntPtr strPtr = ReadFieldPtr(className, fieldName, objectPtr, ns, assemblyName);
        return ReadMonoString(strPtr);
    }

    public string ReadMonoString(IntPtr monoStringPtr)
    {
        if (monoStringPtr == IntPtr.Zero) return string.Empty;
        int lengthOffset = _memory.Is64Bit ? 0x10 : 0x08;
        int charsOffset = _memory.Is64Bit ? 0x14 : 0x0C;

        int length = _memory.ReadInt(monoStringPtr + lengthOffset);
        if (length <= 0 || length > 4096) return string.Empty;

        return _memory.ReadUnicodeString(monoStringPtr + charsOffset, length * 2);
    }

    public string[] ReadMonoStringArray(IntPtr monoArrayPtr)
    {
        IntPtr[] ptrs = ReadMonoPointerArray(monoArrayPtr);
        var result = new string[ptrs.Length];
        for (int i = 0; i < ptrs.Length; i++)
        {
            result[i] = ReadMonoString(ptrs[i]);
        }
        return result;
    }

    public IntPtr[] ReadMonoPointerArray(IntPtr monoArrayPtr)
    {
        if (monoArrayPtr == IntPtr.Zero) return [];

        int lengthOffset = _memory.Is64Bit ? 0x18 : 0x0C;
        int dataOffset = _memory.Is64Bit ? 0x20 : 0x10;

        int length = _memory.ReadInt(monoArrayPtr + lengthOffset);
        if (length <= 0 || length > 10000) return [];

        var result = new IntPtr[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = _memory.ReadIntPtr(monoArrayPtr + dataOffset + (i * _memory.PointerSize));
        }
        return result;
    }

    public void WriteMonoPointerArrayElement(IntPtr monoArrayPtr, int index, IntPtr value)
    {
        if (monoArrayPtr == IntPtr.Zero || index < 0) return;
        int dataOffset = _memory.Is64Bit ? 0x20 : 0x10;
        IntPtr targetAddress = monoArrayPtr + dataOffset + (index * _memory.PointerSize);
        if (_memory.Is64Bit)
        {
            _memory.Write(targetAddress, value.ToInt64());
        }
        else
        {
            _memory.Write(targetAddress, unchecked((int)(long)value));
        }
    }

    public void WriteField<T>(string className, string fieldName, IntPtr objectPtr, T value) where T : unmanaged
    {
        if (objectPtr == IntPtr.Zero) return;
        var info = GetFieldInfo(className, fieldName);
        if (info != null)
        {
            if (typeof(T) == typeof(IntPtr))
            {
                IntPtr ptrVal = (IntPtr)(object)value;
                if (_memory.Is64Bit)
                    _memory.Write(objectPtr + info.Offset, ptrVal.ToInt64());
                else
                    _memory.Write(objectPtr + info.Offset, unchecked((int)(long)ptrVal));
                return;
            }
            _memory.Write(objectPtr + info.Offset, value);
        }
    }

    public void WriteStringField(string className, string fieldName, IntPtr objectPtr, string value)
    {
        IntPtr monoStr = CreateMonoString(value);
        WriteField(className, fieldName, objectPtr, monoStr);
    }

    public void WriteStatic<T>(string className, string fieldName, T value) where T : unmanaged
    {
        var info = GetFieldInfo(className, fieldName);
        if (info == null) return;

        IntPtr vtable = GetVTable(info.ClassPtr);
        if (vtable == IntPtr.Zero) return;

        IntPtr staticData = GetStaticFieldData(vtable);
        if (staticData != IntPtr.Zero)
        {
            if (typeof(T) == typeof(IntPtr))
            {
                IntPtr ptrVal = (IntPtr)(object)value;
                if (_memory.Is64Bit)
                    _memory.Write(staticData + info.Offset, ptrVal.ToInt64());
                else
                    _memory.Write(staticData + info.Offset, unchecked((int)(long)ptrVal));
                return;
            }
            _memory.Write(staticData + info.Offset, value);
            return;
        }

        IntPtr valPtr = typeof(T) == typeof(IntPtr) && !_memory.Is64Bit
            ? _memory.AllocateAndWrite(unchecked((int)(long)(IntPtr)(object)value))
            : _memory.AllocateAndWrite(value);
        try
        {
            _invoker.Call(GetExport(Constants.MonoExports.FieldStaticSetValue), vtable, info.FieldPtr, valPtr);
        }
        finally
        {
            _memory.Free(valPtr);
        }
    }

    public void WriteStaticString(string className, string fieldName, string value)
    {
        IntPtr monoStr = CreateMonoString(value);
        WriteStatic(className, fieldName, monoStr);
    }

    public IntPtr ResolveMethod(string className, string methodName, int paramCount, string ns = "\0", string assemblyName = "Assembly-CSharp")
    {
        var key = (assemblyName, ns, className, methodName, paramCount);
        if (_methodCache.TryGetValue(key, out var cached) && cached != IntPtr.Zero)
            return cached;

        IntPtr classPtr = GetClass(className, ns, assemblyName);
        if (classPtr == IntPtr.Zero) return IntPtr.Zero;

        IntPtr namePtr = _memory.AllocateAndWrite(methodName);
        try
        {
            IntPtr currentClass = classPtr;
            while (currentClass != IntPtr.Zero)
            {
                IntPtr methodPtr = _invoker.Call(
                    GetExport(Constants.MonoExports.ClassGetMethodFromName),
                    currentClass, namePtr, (IntPtr)paramCount);

                if (methodPtr != IntPtr.Zero)
                {
                    _methodCache[key] = methodPtr;
                    return methodPtr;
                }

                currentClass = _invoker.Call(GetExport(Constants.MonoExports.ClassGetParent), currentClass);
            }
            return IntPtr.Zero;
        }
        finally
        {
            _memory.Free(namePtr);
        }
    }

    public IntPtr InvokeMethod(IntPtr methodPtr, IntPtr thisObj, params IntPtr[]? args)
    {
        if (methodPtr == IntPtr.Zero) return IntPtr.Zero;

        IntPtr paramsPtr = IntPtr.Zero;
        IntPtr excPtr = _memory.AllocateAndWrite((long)0);
        try
        {
            if (args != null && args.Length > 0)
                paramsPtr = _memory.AllocateAndWrite(args);

            IntPtr result = _invoker.Call(
                GetExport(Constants.MonoExports.RuntimeInvoke),
                methodPtr, thisObj, paramsPtr, excPtr);

            IntPtr exc = _memory.ReadIntPtr(excPtr);
            if (exc != IntPtr.Zero)
                return IntPtr.Zero;

            return result;
        }
        finally
        {
            if (paramsPtr != IntPtr.Zero)
                _memory.Free(paramsPtr);
            _memory.Free(excPtr);
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
                _memory.Free(ptr);
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
                _memory.Free(ptr);
        }
    }

    public IntPtr CreateMonoString(string value)
    {
        IntPtr strPtr = _memory.AllocateAndWrite(value);
        try
        {
            return _invoker.Call(
                GetExport(Constants.MonoExports.StringNew),
                RootDomain, strPtr);
        }
        finally
        {
            _memory.Free(strPtr);
        }
    }

    public IntPtr CreateInstance(string className, params object?[]? args)
    {
        try
        {
            IntPtr classPtr = GetClass(className);
            if (classPtr == IntPtr.Zero) return IntPtr.Zero;

            IntPtr exportObjNew = GetExport(Constants.MonoExports.ObjectNew);
            if (exportObjNew == IntPtr.Zero) return IntPtr.Zero;

            IntPtr monoObject = _invoker.Call(exportObjNew, RootDomain, classPtr);
            if (monoObject == IntPtr.Zero) return IntPtr.Zero;

            if (args != null && args.Length > 0)
            {
                CallMethod(className, ".ctor", monoObject, args);
            }
            else
            {
                CallMethod(className, ".ctor", monoObject);
            }

            return monoObject;
        }
        catch
        {
            return IntPtr.Zero;
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
                IntPtr ptr = _memory.AllocateAndWrite(b ? (byte)1 : (byte)0);
                tempAllocations.Add(ptr);
                return ptr;
            }
            case byte b:
            {
                IntPtr ptr = _memory.AllocateAndWrite(b);
                tempAllocations.Add(ptr);
                return ptr;
            }
            case sbyte sb:
            {
                IntPtr ptr = _memory.AllocateAndWrite(sb);
                tempAllocations.Add(ptr);
                return ptr;
            }
            case short s:
            {
                IntPtr ptr = _memory.AllocateAndWrite(s);
                tempAllocations.Add(ptr);
                return ptr;
            }
            case ushort us:
            {
                IntPtr ptr = _memory.AllocateAndWrite(us);
                tempAllocations.Add(ptr);
                return ptr;
            }
            case int i:
            {
                IntPtr ptr = _memory.AllocateAndWrite(i);
                tempAllocations.Add(ptr);
                return ptr;
            }
            case uint ui:
            {
                IntPtr ptr = _memory.AllocateAndWrite(ui);
                tempAllocations.Add(ptr);
                return ptr;
            }
            case long l:
            {
                IntPtr ptr = _memory.AllocateAndWrite(l);
                tempAllocations.Add(ptr);
                return ptr;
            }
            case float f:
            {
                IntPtr ptr = _memory.AllocateAndWrite(f);
                tempAllocations.Add(ptr);
                return ptr;
            }
            default:
                throw new NotSupportedException($"Marshalling type {arg.GetType().Name} is not supported.");
        }
    }

    public void Dispose()
    {
        _scratchpad.Dispose();
        _memory.Dispose();
        if (_processHandle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_processHandle);
        }
        GC.SuppressFinalize(this);
    }
}
