using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Execution;
using DragonBoyAccountManager.Core.Interop.Memory;
using DragonBoyAccountManager.Core.Interop.Native;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Interop.Il2Cpp;

public class Il2CppRuntime : IGameRuntime
{
    private readonly IntPtr _processHandle;
    private readonly IMemoryAccessor _memory;
    private readonly IntPtr _gameAssemblyModule;
    private readonly GameVersionSchema _schema;
    private readonly RemoteScratchpad _scratchpad;
    private readonly RemoteInvoker _invoker;
    private readonly Lock _domainInitLock = new();
    private IntPtr _stringKlass = IntPtr.Zero;
    private IntPtr _objectNewAddress = IntPtr.Zero;
    private IntPtr _stringNewAddress = IntPtr.Zero;
    private IntPtr _getStringClassAddress = IntPtr.Zero;
    private IntPtr _gcHandleNewAddress = IntPtr.Zero;
    private IntPtr _gcHandleFreeAddress = IntPtr.Zero;
    private IntPtr _domainGetAddr = IntPtr.Zero;
    private IntPtr _attachAddr = IntPtr.Zero;
    private IntPtr _detachAddr = IntPtr.Zero;

    private readonly ConcurrentDictionary<string, IntPtr> _classPointerCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IntPtr> _staticFieldsPointerCache = new(StringComparer.OrdinalIgnoreCase);

    public IntPtr RootDomain => _invoker.RootDomain;
    public IMemoryAccessor Memory => _memory;
    public IntPtr ProcessHandle => _processHandle;
    public IntPtr ModuleBase => _gameAssemblyModule;
    public bool Is64Bit => _memory.Is64Bit;
    public GameVersionSchema Schema => _schema;
    public GameMemoryLayout Layout { get; }
    public RemoteInvoker Invoker => _invoker;

    public Il2CppRuntime(IntPtr processHandle, IMemoryAccessor memory, IntPtr gameAssemblyModule, GameVersionSchema schema)
    {
        _processHandle = processHandle;
        _memory = memory;
        _gameAssemblyModule = gameAssemblyModule;
        _schema = schema;
        Layout = new GameMemoryLayout(schema);
        _scratchpad = new RemoteScratchpad(processHandle);
        _invoker = new RemoteInvoker(processHandle, memory, _scratchpad);

        LoadExports();
    }

    public static async Task<Il2CppRuntime?> AttachAsync(
        Process? process,
        string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (process == null || process.HasExited)
            return null;

        var handle = NativeMethods.OpenProcess(ProcessAccessRights.AllAccess, false, process.Id);
        if (handle == IntPtr.Zero)
            return null;

        var memory = new ProcessMemory(handle);
        try
        {
            var gameAssembly = await ProcessHelper.WaitForGameAssemblyModuleAsync(handle, memory.Is64Bit, cancellationToken: cancellationToken);
            if (gameAssembly == IntPtr.Zero)
            {
                memory.Dispose();
                NativeMethods.CloseHandle(handle);
                return null;
            }

            var schema = await VersionOffsetProvider.Instance.GetSchemaAsync(targetVersion, cancellationToken: cancellationToken)
                         ?? VersionOffsetProvider.Instance.GetDefaultSchema(targetVersion);

            return new Il2CppRuntime(handle, memory, gameAssembly, schema);
        }
        catch
        {
            memory.Dispose();
            NativeMethods.CloseHandle(handle);
            throw;
        }
    }

    private void LoadExports()
    {
        try
        {
            int e_lfanew = _memory.ReadInt(_gameAssemblyModule + 0x3C);
            IntPtr ntHeaders = _gameAssemblyModule + e_lfanew;
            IntPtr optionalHeader = ntHeaders + 0x18;

            IntPtr exportDataDir = optionalHeader + (_memory.Is64Bit ? 0x70 : 0x60);
            int exportRva = _memory.ReadInt(exportDataDir);
            uint exportSize = _memory.ReadUInt(exportDataDir + 0x04);

            if (exportRva == 0) return;

            IntPtr exportDir = _gameAssemblyModule + exportRva;
            int count = _memory.ReadInt(exportDir + 0x18);
            IntPtr functions = _gameAssemblyModule + _memory.ReadInt(exportDir + 0x1C);
            IntPtr names = _gameAssemblyModule + _memory.ReadInt(exportDir + 0x20);
            IntPtr ordinals = _gameAssemblyModule + _memory.ReadInt(exportDir + 0x24);

            for (int i = 0; i < count; i++)
            {
                int nameRva = _memory.ReadInt(names + i * 4);
                string funcName = _memory.ReadString(_gameAssemblyModule + nameRva, Constants.DefaultStringLength, Encoding.ASCII);

                if (funcName is "il2cpp_domain_get" or "il2cpp_thread_attach" or "il2cpp_thread_detach" or "il2cpp_object_new" or "il2cpp_string_new" or "mono_get_string_class" or "il2cpp_gchandle_new" or "il2cpp_gchandle_free")
                {
                    ushort ordinal = _memory.ReadUShort(ordinals + i * 2);
                    uint funcRva = _memory.ReadUInt(functions + ordinal * 4);

                    if (funcRva >= (uint)exportRva && funcRva < (uint)(exportRva + exportSize))
                        continue;

                    IntPtr funcAddress = _gameAssemblyModule + (int)funcRva;
                    if (funcName == "il2cpp_domain_get") _domainGetAddr = funcAddress;
                    else if (funcName == "il2cpp_thread_attach") _attachAddr = funcAddress;
                    else if (funcName == "il2cpp_thread_detach") _detachAddr = funcAddress;
                    else if (funcName == "il2cpp_object_new") _objectNewAddress = funcAddress;
                    else if (funcName == "il2cpp_string_new") _stringNewAddress = funcAddress;
                    else if (funcName == "mono_get_string_class") _getStringClassAddress = funcAddress;
                    else if (funcName == "il2cpp_gchandle_new") _gcHandleNewAddress = funcAddress;
                    else if (funcName == "il2cpp_gchandle_free") _gcHandleFreeAddress = funcAddress;
                }
            }
        }
        catch
        {
        }
    }

    private void EnsureDomainInitialized()
    {
        if (_invoker.RootDomain != IntPtr.Zero && _stringKlass != IntPtr.Zero) return;
        lock (_domainInitLock)
        {
            if (_invoker.RootDomain == IntPtr.Zero && _domainGetAddr != IntPtr.Zero)
            {
                IntPtr domain = _invoker.Call(_domainGetAddr);
                if (domain != IntPtr.Zero) _invoker.SetDomain(domain, _attachAddr, _detachAddr);
            }
            if (_stringKlass == IntPtr.Zero && _getStringClassAddress != IntPtr.Zero)
            {
                _stringKlass = _invoker.Call(_getStringClassAddress);
            }
        }
    }

    public IntPtr GetClass(string className)
    {
        if (_classPointerCache.TryGetValue(className, out var cachedClass))
            return cachedClass;

        long rva = _schema.GetTypeInfoRva(className);
        if (rva == 0) return IntPtr.Zero;

        IntPtr typeInfoPtr = _gameAssemblyModule + (nint)rva;
        IntPtr classPtr = _memory.ReadIntPtr(typeInfoPtr);
        if (classPtr == IntPtr.Zero)
            return IntPtr.Zero;

        ulong val = (ulong)classPtr;
        if (val < 0x10000 || (val & 0xFF000000) == 0x20000000)
            return IntPtr.Zero;

        _classPointerCache[className] = classPtr;
        return classPtr;
    }

    public IntPtr GetStaticFieldsPtr(string className)
    {
        if (_staticFieldsPointerCache.TryGetValue(className, out var cached))
            return cached;

        IntPtr classPtr = GetClass(className);
        if (classPtr == IntPtr.Zero) return IntPtr.Zero;

        IntPtr staticFieldsPtr = _memory.ReadIntPtr(classPtr + _schema.StaticFieldsOffset);
        if (staticFieldsPtr == IntPtr.Zero)
            return IntPtr.Zero;

        ulong val = (ulong)staticFieldsPtr;
        if (val < 0x10000 || (val & 0xFF000000) == 0x20000000)
            return IntPtr.Zero;

        _staticFieldsPointerCache[className] = staticFieldsPtr;
        return staticFieldsPtr;
    }

    public string? GetClassName(IntPtr objectPtr)
    {
        if (objectPtr == IntPtr.Zero) return null;
        IntPtr classPtr = _memory.ReadIntPtr(objectPtr);
        if (classPtr == IntPtr.Zero) return null;
        IntPtr namePtr = _memory.ReadIntPtr(classPtr + 0x10);
        if (namePtr == IntPtr.Zero) return null;
        return _memory.ReadUTF8String(namePtr, 64);
    }

    public T ReadStatic<T>(string className, int offset) where T : unmanaged
    {
        if (offset < 0) return default;
        IntPtr staticFieldsPtr = GetStaticFieldsPtr(className);
        if (staticFieldsPtr == IntPtr.Zero) return default;
        return _memory.Read<T>(staticFieldsPtr + offset);
    }

    public string ReadStaticString(string className, int offset)
    {
        if (offset < 0) return string.Empty;
        IntPtr staticFieldsPtr = GetStaticFieldsPtr(className);
        if (staticFieldsPtr == IntPtr.Zero) return string.Empty;
        IntPtr strPtr = _memory.ReadIntPtr(staticFieldsPtr + offset);
        return ReadString(strPtr);
    }

    public T ReadField<T>(IntPtr objectPtr, int offset) where T : unmanaged
    {
        if (objectPtr == IntPtr.Zero || offset < 0) return default;
        return _memory.Read<T>(objectPtr + offset);
    }

    public IntPtr ReadFieldPtr(IntPtr objectPtr, int offset)
    {
        if (objectPtr == IntPtr.Zero || offset < 0) return IntPtr.Zero;
        return _memory.ReadIntPtr(objectPtr + offset);
    }

    public string ReadStringField(IntPtr objectPtr, int offset)
    {
        IntPtr strPtr = ReadFieldPtr(objectPtr, offset);
        return ReadString(strPtr);
    }

    public string ReadString(IntPtr stringPtr)
    {
        if (stringPtr == IntPtr.Zero) return string.Empty;
        if (_stringKlass == IntPtr.Zero)
        {
            IntPtr klass = _memory.ReadIntPtr(stringPtr);
            if (klass != IntPtr.Zero) _stringKlass = klass;
        }
        int length = _memory.ReadInt(stringPtr + 0x10);
        if (length <= 0 || length > 10000) return string.Empty;

        return _memory.ReadUnicodeString(stringPtr + 0x14, length * 2);
    }

    public string[] ReadStringArray(IntPtr arrayPtr)
    {
        IntPtr[] ptrs = ReadPointerArray(arrayPtr);
        if (ptrs.Length == 0) return [];

        var res = new string[ptrs.Length];
        for (int i = 0; i < ptrs.Length; i++)
        {
            res[i] = ReadString(ptrs[i]);
        }
        return res;
    }

    public int[] ReadIntArray(IntPtr arrayPtr, int maxLength = 1_000_000)
    {
        if (arrayPtr == IntPtr.Zero) return [];
        int length = _memory.ReadInt(arrayPtr + 0x18);
        if (length <= 0) return [];
        length = Math.Min(length, maxLength);

        byte[] raw = _memory.ReadBytes(arrayPtr + 0x20, length * sizeof(int));
        int actualCount = raw.Length / sizeof(int);
        if (actualCount <= 0) return [];

        var res = new int[actualCount];
        Buffer.BlockCopy(raw, 0, res, 0, actualCount * sizeof(int));
        return res;
    }

    public IntPtr[] ReadPointerArray(IntPtr arrayPtr)
    {
        if (arrayPtr == IntPtr.Zero) return [];
        int length = _memory.ReadInt(arrayPtr + 0x18);
        if (length <= 0 || length > 10000) return [];

        int elemSize = _memory.PointerSize;
        byte[] raw = _memory.ReadBytes(arrayPtr + 0x20, length * elemSize);
        int actualCount = raw.Length / elemSize;
        if (actualCount <= 0) return [];

        var res = new IntPtr[actualCount];
        for (int i = 0; i < actualCount; i++)
        {
            res[i] = elemSize == 8
                ? (IntPtr)BitConverter.ToInt64(raw, i * 8)
                : (IntPtr)BitConverter.ToInt32(raw, i * 4);
        }
        return res;
    }

    public void WritePointerArrayElement(IntPtr arrayPtr, int index, IntPtr value)
    {
        if (arrayPtr == IntPtr.Zero || index < 0) return;
        int length = _memory.ReadInt(arrayPtr + 0x18);
        if (index >= length) return;

        _memory.Write(arrayPtr + 0x20 + (index * 8), value);
    }

    public void WriteField<T>(IntPtr objectPtr, int offset, T value) where T : unmanaged
    {
        if (objectPtr == IntPtr.Zero || offset < 0) return;
        _memory.Write(objectPtr + offset, value);
    }

    public unsafe void WriteStringField(IntPtr objectPtr, int offset, string value)
    {
        if (objectPtr == IntPtr.Zero || offset < 0) return;
        IntPtr currentStr = ReadFieldPtr(objectPtr, offset);
        if (currentStr != IntPtr.Zero)
        {
            IntPtr klass = _memory.ReadIntPtr(currentStr);
            if (klass != IntPtr.Zero) _stringKlass = klass;

            int curLen = _memory.ReadInt(currentStr + 0x10);
            if (value.Length <= curLen)
            {
                _memory.Write(currentStr + 0x10, value.Length);
                fixed (char* p = value)
                {
                    NativeMethods.WriteProcessMemory(_processHandle, currentStr + 0x14, p, (nuint)(value.Length * 2), out _);
                }
                _memory.Write<short>(currentStr + 0x14 + (value.Length * 2), 0);
                return;
            }
        }

        IntPtr newStr = CreateString(value);
        if (newStr != IntPtr.Zero)
        {
            _memory.Write(objectPtr + offset, newStr);
        }
    }

    public void WriteStatic<T>(string className, int offset, T value) where T : unmanaged
    {
        if (offset < 0) return;
        IntPtr staticFields = GetStaticFieldsPtr(className);
        if (staticFields == IntPtr.Zero) return;
        _memory.Write(staticFields + offset, value);
    }

    public unsafe void WriteStaticString(string className, int offset, string value)
    {
        if (offset < 0) return;
        IntPtr staticFields = GetStaticFieldsPtr(className);
        if (staticFields == IntPtr.Zero) return;

        IntPtr currentStr = _memory.ReadIntPtr(staticFields + offset);
        if (currentStr != IntPtr.Zero)
        {
            IntPtr klass = _memory.ReadIntPtr(currentStr);
            if (klass != IntPtr.Zero) _stringKlass = klass;

            int curLen = _memory.ReadInt(currentStr + 0x10);
            if (value.Length <= curLen)
            {
                _memory.Write(currentStr + 0x10, value.Length);
                fixed (char* p = value)
                {
                    NativeMethods.WriteProcessMemory(_processHandle, currentStr + 0x14, p, (nuint)(value.Length * 2), out _);
                }
                _memory.Write<short>(currentStr + 0x14 + (value.Length * 2), 0);
                return;
            }
        }

        IntPtr newStr = CreateString(value);
        if (newStr != IntPtr.Zero)
        {
            _memory.Write(staticFields + offset, newStr);
        }
    }

    public IntPtr ResolveMethod(string className, string methodName, int paramCount = 0)
    {
        long rva = _schema.GetMethodRva(className, methodName);
        return rva > 0 ? _gameAssemblyModule + (nint)rva : IntPtr.Zero;
    }

    public IntPtr CallMethod(string className, string methodName, IntPtr thisObj, params object?[]? args)
    {
        EnsureDomainInitialized();
        IntPtr methodPtr = ResolveMethod(className, methodName, args?.Length ?? 0);
        if (methodPtr == IntPtr.Zero) return IntPtr.Zero;

        var convertedArgs = new List<IntPtr>();
        if (thisObj != IntPtr.Zero)
        {
            convertedArgs.Add(thisObj);
        }

        if (args != null)
        {
            foreach (var arg in args)
            {
                switch (arg)
                {
                    case null:
                        convertedArgs.Add(IntPtr.Zero);
                        break;
                    case IntPtr ptr:
                        convertedArgs.Add(ptr);
                        break;
                    case int i:
                        convertedArgs.Add((IntPtr)i);
                        break;
                    case uint ui:
                        convertedArgs.Add((IntPtr)(long)ui);
                        break;
                    case long l:
                        convertedArgs.Add((IntPtr)l);
                        break;
                    case bool b:
                        convertedArgs.Add((IntPtr)(b ? 1 : 0));
                        break;
                    case sbyte sb:
                        convertedArgs.Add((IntPtr)sb);
                        break;
                    case byte bt:
                        convertedArgs.Add((IntPtr)(long)bt);
                        break;
                    case short sh:
                        convertedArgs.Add((IntPtr)sh);
                        break;
                    case ushort ush:
                        convertedArgs.Add((IntPtr)(long)ush);
                        break;
                    case float f:
                        convertedArgs.Add((IntPtr)BitConverter.SingleToInt32Bits(f));
                        break;
                    case string s:
                        convertedArgs.Add(CreateString(s));
                        break;
                    default:
                        convertedArgs.Add(IntPtr.Zero);
                        break;
                }
            }
        }

        return _invoker.Call(methodPtr, [.. convertedArgs]);
    }

    public unsafe IntPtr CreateString(string value)
    {
        EnsureDomainInitialized();
        if (string.IsNullOrEmpty(value)) return IntPtr.Zero;

        if (_stringNewAddress != IntPtr.Zero)
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(value);
            var strPtr = _scratchpad.WithLock(pad =>
            {
                fixed (byte* p = utf8Bytes)
                {
                    NativeMethods.WriteProcessMemory(_processHandle, pad.StringDataBuffer, p, (nuint)utf8Bytes.Length, out _);
                }
                _memory.Write<byte>(pad.StringDataBuffer + utf8Bytes.Length, 0);
                return _invoker.Call(_stringNewAddress, pad.StringDataBuffer);
            });

            if (strPtr != IntPtr.Zero)
            {
                IntPtr klass = _memory.ReadIntPtr(strPtr);
                if (klass != IntPtr.Zero) _stringKlass = klass;
                return strPtr;
            }
        }

        if (_stringKlass == IntPtr.Zero && _getStringClassAddress != IntPtr.Zero)
        {
            _stringKlass = _invoker.Call(_getStringClassAddress);
        }

        if (_stringKlass == IntPtr.Zero) return IntPtr.Zero;

        int byteCount = (value.Length + 1) * 2;
        int totalSize = 0x14 + byteCount;
        IntPtr alloc = _memory.Allocate(totalSize);
        if (alloc == IntPtr.Zero) return IntPtr.Zero;

        _memory.Write(alloc, _stringKlass);
        _memory.Write(alloc + 8, IntPtr.Zero);
        _memory.Write(alloc + 0x10, value.Length);

        fixed (char* p = value)
        {
            NativeMethods.WriteProcessMemory(_processHandle, alloc + 0x14, p, (nuint)byteCount, out _);
        }
        _memory.Write<short>(alloc + 0x14 + (value.Length * 2), 0);

        return alloc;
    }

    public IntPtr CreateInstance(string className, params object?[]? args)
    {
        EnsureDomainInitialized();
        try
        {
            if (_objectNewAddress == IntPtr.Zero) return IntPtr.Zero;

            IntPtr classPtr = GetClass(className);
            if (classPtr == IntPtr.Zero) return IntPtr.Zero;

            IntPtr obj = _invoker.Call(_objectNewAddress, classPtr);
            if (obj == IntPtr.Zero) return IntPtr.Zero;

            if (args != null && args.Length > 0)
            {
                CallMethod(className, ".ctor", obj, args);
            }
            else
            {
                CallMethod(className, ".ctor", obj);
            }

            return obj;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public uint PinObject(IntPtr objectPtr)
    {
        EnsureDomainInitialized();
        if (objectPtr == IntPtr.Zero || _gcHandleNewAddress == IntPtr.Zero) return 0;
        return (uint)_invoker.Call(_gcHandleNewAddress, objectPtr, (IntPtr)1);
    }

    public void UnpinObject(uint gcHandle)
    {
        if (gcHandle == 0 || _gcHandleFreeAddress == IntPtr.Zero) return;
        EnsureDomainInitialized();
        _ = _invoker.Call(_gcHandleFreeAddress, (IntPtr)gcHandle);
    }

    public void Dispose()
    {
        _scratchpad.Dispose();
        _memory.Dispose();
        NativeMethods.CloseHandle(_processHandle);
        GC.SuppressFinalize(this);
    }
}
