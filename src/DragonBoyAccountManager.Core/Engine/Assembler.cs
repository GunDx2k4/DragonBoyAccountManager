namespace DragonBoyAccountManager.Core.Engine;

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

    public void Push(IntPtr arg)
    {
        int val = unchecked((int)(long)arg);
        if (val >= -128 && val <= 127)
        {
            _asm.Add(0x6A);
            _asm.Add((byte)(sbyte)val);
        }
        else
        {
            _asm.Add(0x68);
            _asm.AddRange(BitConverter.GetBytes(val));
        }
    }

    public void MovEax(IntPtr arg)
    {
        _asm.Add(0xB8);
        _asm.AddRange(BitConverter.GetBytes(unchecked((int)(long)arg)));
    }

    public void MovEcx(IntPtr arg)
    {
        _asm.Add(0xB9);
        _asm.AddRange(BitConverter.GetBytes(unchecked((int)(long)arg)));
    }

    public void MovEdx(IntPtr arg)
    {
        _asm.Add(0xBA);
        _asm.AddRange(BitConverter.GetBytes(unchecked((int)(long)arg)));
    }

    public void CallEax()
    {
        _asm.AddRange([0xFF, 0xD0]);
    }

    public void AddEsp(byte arg)
    {
        _asm.AddRange([0x83, 0xC4, arg]);
    }

    public void MovEaxTo(IntPtr dest)
    {
        _asm.Add(0xA3);
        _asm.AddRange(BitConverter.GetBytes(unchecked((int)(long)dest)));
    }

    public void Return()
    {
        _asm.Add(0xC3);
    }

    public byte[] ToByteArray() => _asm.ToArray();
}
