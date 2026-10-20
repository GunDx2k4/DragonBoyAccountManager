using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace DragonBoyAccountManager.Core.Infrastructure.Mono;

public class MonoContext : IDisposable
{
    private readonly IntPtr _handle;
    private readonly ProcessMemory _memory;
    private readonly IntPtr _monoModule;
    private readonly bool _is64Bit;
    private readonly Dictionary<string, IntPtr> _exports = new();
    private bool _disposed;

    public Process Process { get; }
    public ProcessMemory Memory => _memory;
    public MonoInvoker Invoker { get; }
    public IntPtr RootDomain { get; private set; }

    private static readonly HashSet<string> DesiredExports = typeof(Constants.MonoExports)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
        .Select(f => (string)f.GetValue(null)!)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private MonoContext(Process process, IntPtr handle, ProcessMemory memory, IntPtr monoModule)
    {
        Process = process;
        _handle = handle;
        _memory = memory;
        _monoModule = monoModule;
        _is64Bit = memory.Is64Bit;
        Invoker = new MonoInvoker(handle, memory);
    }

    public static async Task<MonoContext?> CreateAsync(Process? process, CancellationToken cancellationToken = default)
    {
        if (process == null || process.HasExited)
            return null;

        var handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, process.Id);
        if (handle == IntPtr.Zero)
            return null;

        var memory = new ProcessMemory(handle);
        try
        {
            var monoModule = await ProcessHelper.WaitForMonoModuleAsync(handle, memory.Is64Bit, cancellationToken: cancellationToken);
            if (monoModule == IntPtr.Zero)
            {
                memory.Dispose();
                Native.CloseHandle(handle);
                return null;
            }

            await Task.Delay(1000, cancellationToken);

            var context = new MonoContext(process, handle, memory, monoModule);
            context.LoadExports();

            if (!await context.InitializeRootDomainAsync(cancellationToken))
            {
                context.Dispose();
                return null;
            }

            return context;
        }
        catch
        {
            memory.Dispose();
            Native.CloseHandle(handle);
            throw;
        }
    }

    private void LoadExports()
    {
        int e_lfanew = _memory.ReadInt(_monoModule + 0x3C);
        IntPtr ntHeaders = _monoModule + e_lfanew;
        IntPtr optionalHeader = ntHeaders + 0x18;

        IntPtr exportDataDir = optionalHeader + (_is64Bit ? 0x70 : 0x60);
        IntPtr exportRva = _memory.ReadInt(exportDataDir);
        uint exportSize = _memory.ReadUInt(exportDataDir + 0x04);

        if (exportRva == 0)
            return;

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

    private async Task<bool> InitializeRootDomainAsync(CancellationToken cancellationToken)
    {
        IntPtr getDomainAddr = GetExport(Constants.MonoExports.RootDomain);
        IntPtr threadAttachAddr = GetExport(Constants.MonoExports.ThreadAttach);
        IntPtr threadDetachAddr = GetExport(Constants.MonoExports.ThreadDetach);

        if (getDomainAddr == IntPtr.Zero || threadAttachAddr == IntPtr.Zero)
            return false;

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 15000 && !cancellationToken.IsCancellationRequested)
        {
            if (!ProcessHelper.IsProcessAlive(_handle))
                return false;

            RootDomain = Invoker.Call(getDomainAddr);
            if (RootDomain != IntPtr.Zero)
            {
                await Task.Delay(200, cancellationToken);
                Invoker.SetDomain(RootDomain, threadAttachAddr, threadDetachAddr);
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _memory.Dispose();
        if (_handle != IntPtr.Zero)
            Native.CloseHandle(_handle);
    }
}
