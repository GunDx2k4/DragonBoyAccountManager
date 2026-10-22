using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public static class CollectionHelper
{
    public static List<IntPtr> ReadMyVector(IMonoRuntime mono, IntPtr vectorPtr)
    {
        var result = new List<IntPtr>();
        if (vectorPtr == IntPtr.Zero) return result;

        try
        {
            IntPtr arrayListPtr = mono.ReadFieldPtr(GameSchema.MyVector.ClassName, GameSchema.MyVector.FieldA, vectorPtr);
            if (arrayListPtr != IntPtr.Zero)
            {
                var sizeField = mono.GetFieldInfo(GameSchema.ArrayList.ClassName, GameSchema.ArrayList.FieldSize, GameSchema.Namespaces.SystemCollections, GameSchema.Namespaces.Mscorlib)
                             ?? mono.GetFieldInfo(GameSchema.ArrayList.ClassName, GameSchema.ArrayList.FieldSizeAlt, GameSchema.Namespaces.SystemCollections, GameSchema.Namespaces.Mscorlib);
                var itemsField = mono.GetFieldInfo(GameSchema.ArrayList.ClassName, GameSchema.ArrayList.FieldItems, GameSchema.Namespaces.SystemCollections, GameSchema.Namespaces.Mscorlib)
                              ?? mono.GetFieldInfo(GameSchema.ArrayList.ClassName, GameSchema.ArrayList.FieldItemsAlt, GameSchema.Namespaces.SystemCollections, GameSchema.Namespaces.Mscorlib);

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
                            if (actualCount <= 0) return result;

                            int elemOffset = ptrSize * 4;
                            byte[] raw = mono.Memory.ReadBytes(itemsArrayPtr + elemOffset, actualCount * ptrSize);
                            if (raw.Length < actualCount * ptrSize) return result;

                            for (int i = 0; i < actualCount; i++)
                            {
                                IntPtr elem = mono.Memory.Is64Bit
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
        }
        catch
        {
        }

        return result;
    }
}
