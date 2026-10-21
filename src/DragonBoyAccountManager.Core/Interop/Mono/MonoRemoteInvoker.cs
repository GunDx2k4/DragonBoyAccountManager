using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Interop.Memory;
using DragonBoyAccountManager.Core.Interop.Native;

namespace DragonBoyAccountManager.Core.Interop.Mono;

public class MonoRemoteInvoker(IntPtr processHandle, IMemoryAccessor memory, RemoteScratchpad scratchpad)
{
    private readonly IntPtr _processHandle = processHandle;
    private readonly IMemoryAccessor _memory = memory;
    private readonly RemoteScratchpad _scratchpad = scratchpad;
    private readonly bool _is64Bit = memory.Is64Bit;
    private readonly Assembler _assembler = new();

    public IntPtr RootDomain { get; private set; } = IntPtr.Zero;
    public IntPtr ThreadAttachAddress { get; private set; } = IntPtr.Zero;
    public IntPtr ThreadDetachAddress { get; private set; } = IntPtr.Zero;

    public void SetDomain(IntPtr rootDomain, IntPtr threadAttachAddress, IntPtr threadDetachAddress = default)
    {
        RootDomain = rootDomain;
        ThreadAttachAddress = threadAttachAddress;
        ThreadDetachAddress = threadDetachAddress;
    }

    public unsafe IntPtr Call(IntPtr functionAddress, params IntPtr[] args)
    {
        if (functionAddress == IntPtr.Zero || !ProcessHelper.IsProcessAlive(_processHandle))
            return IntPtr.Zero;

        return _scratchpad.WithLock(pad =>
        {
            IntPtr retValPtr = pad.ReturnValueBuffer;
            IntPtr shellcodePtr = pad.ShellcodeBuffer;

            _memory.Write(retValPtr, (long)0);

            byte[] code = _is64Bit
                ? Assemble64(functionAddress, retValPtr, args)
                : Assemble86(functionAddress, retValPtr, args);

            fixed (byte* pCode = code)
            {
                NativeMethods.WriteProcessMemory(_processHandle, shellcodePtr, pCode, (nuint)code.Length, out _);
            }

            IntPtr thread = NativeMethods.CreateRemoteThread(
                _processHandle,
                IntPtr.Zero,
                0,
                shellcodePtr,
                IntPtr.Zero,
                ThreadCreationFlags.None,
                out _);

            if (thread == IntPtr.Zero)
                return IntPtr.Zero;

            try
            {
                WaitResult result = NativeMethods.WaitForSingleObject(thread, Constants.DefaultTimeoutMs);
                if (result != WaitResult.Signaled)
                    return IntPtr.Zero;

                IntPtr ret = _memory.ReadIntPtr(retValPtr);
                if (ret == unchecked((IntPtr)(long)0x00000000C0000005))
                    return IntPtr.Zero;

                return ret;
            }
            finally
            {
                NativeMethods.CloseHandle(thread);
            }
        });
    }

    private byte[] Assemble86(IntPtr functionPtr, IntPtr retValPtr, IntPtr[] args)
    {
        _assembler.Clear();

        bool needAttach = RootDomain != IntPtr.Zero && ThreadAttachAddress != IntPtr.Zero;
        if (needAttach)
        {
            _assembler.PushEbx();
            _assembler.Push(RootDomain);
            _assembler.MovEax(ThreadAttachAddress);
            _assembler.CallEax();
            _assembler.AddEsp(4);
            _assembler.MovEbxEax();
        }

        for (int i = args.Length - 1; i >= 0; i--)
        {
            _assembler.Push(args[i]);
        }

        _assembler.MovEax(functionPtr);
        _assembler.CallEax();
        if (args.Length > 0)
        {
            _assembler.AddEsp((byte)(args.Length * 4));
        }
        _assembler.MovEaxTo(retValPtr);

        if (needAttach)
        {
            if (ThreadDetachAddress != IntPtr.Zero)
            {
                _assembler.TestEbxEbx();
                _assembler.Jz(11);
                _assembler.PushEbx();
                _assembler.MovEax(ThreadDetachAddress);
                _assembler.CallEax();
                _assembler.AddEsp(4);
            }
            _assembler.PopEbx();
        }

        _assembler.Return();
        return _assembler.ToByteArray();
    }

    private byte[] Assemble64(IntPtr functionPtr, IntPtr retValPtr, IntPtr[] args)
    {
        _assembler.Clear();

        bool needAttach = RootDomain != IntPtr.Zero && ThreadAttachAddress != IntPtr.Zero;
        if (needAttach)
        {
            _assembler.PushR12();
        }

        int slots = Math.Max(4, args.Length);
        if (slots % 2 == 0) slots++;
        byte allocStack = (byte)(slots * 8);

        _assembler.SubRsp(allocStack);

        if (needAttach)
        {
            _assembler.MovRax(ThreadAttachAddress);
            _assembler.MovRcx(RootDomain);
            _assembler.CallRax();
            _assembler.MovR12Rax();
        }

        if (args.Length > 0) _assembler.MovRcx(args[0]);
        if (args.Length > 1) _assembler.MovRdx(args[1]);
        if (args.Length > 2) _assembler.MovR8(args[2]);
        if (args.Length > 3) _assembler.MovR9(args[3]);

        for (int i = 4; i < args.Length; i++)
        {
            _assembler.MovRax(args[i]);
            _assembler.MovRaxToRspOffset((byte)(0x20 + (i - 4) * 8));
        }

        _assembler.MovRax(functionPtr);
        _assembler.CallRax();
        _assembler.MovRaxTo(retValPtr);

        _assembler.AddRsp(allocStack);

        if (needAttach)
        {
            if (ThreadDetachAddress != IntPtr.Zero)
            {
                _assembler.MovRax(ThreadDetachAddress);
                _assembler.MovRcxR12();
                _assembler.CallRax();
            }
            _assembler.PopR12();
        }

        _assembler.Return();
        return _assembler.ToByteArray();
    }
}
