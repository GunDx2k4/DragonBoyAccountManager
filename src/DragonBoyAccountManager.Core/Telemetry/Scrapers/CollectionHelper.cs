using DragonBoyAccountManager.Core.Interop.Mono;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public static class CollectionHelper
{
    public static List<IntPtr> ReadMyVector(IMonoRuntime mono, IntPtr vectorPtr)
    {
        var result = new List<IntPtr>();
        if (vectorPtr == IntPtr.Zero) return result;

        try
        {
            IntPtr arrayListPtr = mono.ReadFieldPtr("MyVector", "a", vectorPtr);
            if (arrayListPtr != IntPtr.Zero)
            {
                var sizeField = mono.GetFieldInfo("ArrayList", "_size", "System.Collections", "mscorlib")
                             ?? mono.GetFieldInfo("ArrayList", "size", "System.Collections", "mscorlib");
                var itemsField = mono.GetFieldInfo("ArrayList", "_items", "System.Collections", "mscorlib")
                              ?? mono.GetFieldInfo("ArrayList", "items", "System.Collections", "mscorlib");

                if (sizeField != null && itemsField != null)
                {
                    int count = mono.Memory.ReadInt(arrayListPtr + sizeField.Offset);
                    if (count > 0 && count <= 2000)
                    {
                        IntPtr itemsArrayPtr = mono.Memory.ReadIntPtr(arrayListPtr + itemsField.Offset);
                        if (itemsArrayPtr != IntPtr.Zero)
                        {
                            int ptrSize = mono.Memory.PointerSize;
                            int arrayLen = mono.Memory.ReadInt(itemsArrayPtr + ptrSize * 3);
                            int actualCount = Math.Min(count, Math.Max(0, arrayLen));
                            int elemOffset = ptrSize * 4;

                            for (int i = 0; i < actualCount; i++)
                            {
                                IntPtr elem = mono.Memory.ReadIntPtr(itemsArrayPtr + elemOffset + i * ptrSize);
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
        }
        catch
        {
        }

        return result;
    }
}
