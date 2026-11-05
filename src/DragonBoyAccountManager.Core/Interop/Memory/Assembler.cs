namespace DragonBoyAccountManager.Core.Interop.Memory;

public class Assembler
{
    private readonly List<byte> _asm = new();

    public void Clear() => _asm.Clear();

    public void MovRax(IntPtr arg)
    {
        _asm.AddRange([0x48, 0xB8]);
        _asm.AddRange(BitConverter.GetBytes((long)arg));
    }

    public void MovRcx(IntPtr arg)
    {
        _asm.AddRange([0x48, 0xB9]);
        _asm.AddRange(BitConverter.GetBytes((long)arg));
    }

    public void MovRdx(IntPtr arg)
    {
        _asm.AddRange([0x48, 0xBA]);
        _asm.AddRange(BitConverter.GetBytes((long)arg));
    }

    public void MovR8(IntPtr arg)
    {
        _asm.AddRange([0x49, 0xB8]);
        _asm.AddRange(BitConverter.GetBytes((long)arg));
    }

    public void MovR9(IntPtr arg)
    {
        _asm.AddRange([0x49, 0xB9]);
        _asm.AddRange(BitConverter.GetBytes((long)arg));
    }

    public void SubRsp(byte arg)
    {
        _asm.AddRange([0x48, 0x83, 0xEC, arg]);
    }

    public void CallRax()
    {
        _asm.AddRange([0xFF, 0xD0]);
    }

    public void AddRsp(byte arg)
    {
        _asm.AddRange([0x48, 0x83, 0xC4, arg]);
    }

    public void MovRaxTo(IntPtr dest)
    {
        _asm.AddRange([0x48, 0xA3]);
        _asm.AddRange(BitConverter.GetBytes((long)dest));
    }

    public void MovRaxToRspOffset(byte offset)
    {
        _asm.AddRange([0x48, 0x89, 0x44, 0x24, offset]);
    }

    public void PushR12() => _asm.AddRange([0x41, 0x54]);
    public void PopR12() => _asm.AddRange([0x41, 0x5C]);
    public void MovR12Rax() => _asm.AddRange([0x49, 0x89, 0xC4]);
    public void MovRcxR12() => _asm.AddRange([0x4C, 0x89, 0xE1]);
    public void MovdXmm0Rcx() => _asm.AddRange([0x66, 0x0F, 0x6E, 0xC1]);
    public void MovdXmm1Rdx() => _asm.AddRange([0x66, 0x0F, 0x6E, 0xCA]);

    public void Return()
    {
        _asm.Add(0xC3);
    }

    public byte[] ToByteArray() => _asm.ToArray();
}
