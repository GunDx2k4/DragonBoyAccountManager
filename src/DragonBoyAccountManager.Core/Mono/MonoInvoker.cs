using DragonBoyAccountManager.Core.Common;
using DragonBoyAccountManager.Core.Engine;
using DragonBoyAccountManager.Core.Engine.Win32;

namespace DragonBoyAccountManager.Core.Mono;

public class MonoInvoker(IntPtr handle, ProcessMemory memory)
{
    private readonly IntPtr _handle = handle;
    private readonly ProcessMemory _memory = memory;
    private readonly bool _is64Bit = memory.Is64Bit;
    private readonly object _syncLock = new();
    private readonly Assembler _assembler = new();

    public IntPtr RootDomain { get; private set; } = IntPtr.Zero;
    public IntPtr ThreadAttachAddress { get; private set; } = IntPtr.Zero;

    public void SetDomain(IntPtr rootDomain, IntPtr threadAttachAddress)
    {
        RootDomain = rootDomain;
        ThreadAttachAddress = threadAttachAddress;
    }

    public IntPtr Call(IntPtr functionAddress, params IntPtr[] args)
    {
        lock (_syncLock)
        {
            if (functionAddress == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr retValPtr = _is64Bit ? _memory.AllocateAndWrite((long)0) : _memory.AllocateAndWrite(0);
            IntPtr alloc = IntPtr.Zero;
            IntPtr thread = IntPtr.Zero;
            bool callSucceeded = false;

            try
            {
                byte[] code = _is64Bit ? Assemble64(functionAddress, retValPtr, args) : Assemble86(functionAddress, retValPtr, args);

                alloc = _memory.AllocateAndWrite(code);

                thread = Native.CreateRemoteThread(_handle, IntPtr.Zero, 0, alloc, IntPtr.Zero, ThreadCreationFlags.None, out _);
                if (thread == IntPtr.Zero)
                    return IntPtr.Zero;

                WaitResult result = Native.WaitForSingleObject(thread, Constants.DefaultTimeoutMs);

                if (result == WaitResult.WAIT_FAILED || result == WaitResult.WAIT_TIMEOUT)
                    return IntPtr.Zero;

                IntPtr ret = _memory.ReadIntPtr(retValPtr);

                if (ret == 0x00000000C0000005)
                    return IntPtr.Zero;

                callSucceeded = true;
                return ret;
            }
            finally
            {
                if (thread != IntPtr.Zero)
                    Native.CloseHandle(thread);

                if (callSucceeded || thread == IntPtr.Zero)
                {
                    if (alloc != IntPtr.Zero)
                        _memory.Free(alloc);
                    if (retValPtr != IntPtr.Zero)
                        _memory.Free(retValPtr);
                }
            }
        }
    }

    private byte[] Assemble86(IntPtr functionPtr, IntPtr retValPtr, IntPtr[] args)
    {
        _assembler.Clear();

        bool needAttach = RootDomain != IntPtr.Zero && ThreadAttachAddress != IntPtr.Zero;
        if (needAttach)
        {
            _assembler.Push(RootDomain);
            _assembler.MovEax(ThreadAttachAddress);
            _assembler.CallEax();
            _assembler.AddEsp(4);
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
        _assembler.Return();

        return _assembler.ToByteArray();
    }

    private byte[] Assemble64(IntPtr functionPtr, IntPtr retValPtr, IntPtr[] args)
    {
        _assembler.Clear();

        int slots = Math.Max(4, args.Length);
        if (slots % 2 == 0) slots++;
        byte allocStack = (byte)(slots * 8);

        _assembler.SubRsp(allocStack);

        bool needAttach = RootDomain != IntPtr.Zero && ThreadAttachAddress != IntPtr.Zero;
        if (needAttach)
        {
            _assembler.MovRax(ThreadAttachAddress);
            _assembler.MovRcx(RootDomain);
            _assembler.CallRax();
        }

        for (int i = 0; i < Math.Min(4, args.Length); i++)
        {
            switch (i)
            {
                case 0: _assembler.MovRcx(args[i]); break;
                case 1: _assembler.MovRdx(args[i]); break;
                case 2: _assembler.MovR8(args[i]); break;
                case 3: _assembler.MovR9(args[i]); break;
            }
        }

        for (int i = 4; i < args.Length; i++)
        {
            _assembler.MovRax(args[i]);
            _assembler.MovRaxToRspOffset((byte)(0x20 + (i - 4) * 8));
        }

        _assembler.MovRax(functionPtr);
        _assembler.CallRax();
        _assembler.AddRsp(allocStack);
        _assembler.MovRaxTo(retValPtr);
        _assembler.Return();

        return _assembler.ToByteArray();
    }
}
