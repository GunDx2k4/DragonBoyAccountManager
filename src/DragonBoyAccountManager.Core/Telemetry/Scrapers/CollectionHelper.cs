using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public static class CollectionHelper
{
    public static List<IntPtr> ReadMyVector(IGameRuntime runtime, IntPtr vectorPtr)
    {
        var result = new List<IntPtr>();
        if (vectorPtr == IntPtr.Zero) return result;

        try
        {
            IntPtr arrayListPtr = runtime.ReadFieldPtr(vectorPtr, runtime.Layout.MyVector.A);
            if (arrayListPtr != IntPtr.Zero)
            {
                int itemsOffset = runtime.Is64Bit ? 0x10 : 0x08;
                int sizeOffset = runtime.Is64Bit ? 0x18 : 0x0C;

                int count = runtime.Memory.ReadInt(arrayListPtr + sizeOffset);
                if (count > 0 && count <= 2000)
                {
                    IntPtr itemsArrayPtr = runtime.Memory.ReadIntPtr(arrayListPtr + itemsOffset);
                    if (itemsArrayPtr != IntPtr.Zero)
                    {
                        int ptrSize = runtime.Memory.PointerSize;
                        int arrayLen = runtime.Memory.ReadInt(itemsArrayPtr + ptrSize * 3);
                        int actualCount = Math.Min(count, Math.Max(0, arrayLen));
                        if (actualCount <= 0) return result;

                        int elemOffset = ptrSize * 4;
                        byte[] raw = runtime.Memory.ReadBytes(itemsArrayPtr + elemOffset, actualCount * ptrSize);
                        if (raw.Length < actualCount * ptrSize) return result;

                        for (int i = 0; i < actualCount; i++)
                        {
                            IntPtr elem = runtime.Memory.Is64Bit
                                ? (IntPtr)BitConverter.ToInt64(raw, i * 8)
                                : (IntPtr)BitConverter.ToInt32(raw, i * 4);
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
        catch
        {
        }

        return result;
    }
}
