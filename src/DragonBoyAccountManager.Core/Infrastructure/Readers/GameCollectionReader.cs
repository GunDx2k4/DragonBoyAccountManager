
namespace DragonBoyAccountManager.Core.Infrastructure.Readers;

public static class GameCollectionReader
{
    public static List<IntPtr> ReadMyVector(this MonoReflector reflector, IntPtr vectorPtr)
    {
        var result = new List<IntPtr>();
        if (vectorPtr == IntPtr.Zero) return result;

        try
        {
            IntPtr arrayListPtr = reflector.ReadFieldPtr("MyVector", "a", vectorPtr);
            if (arrayListPtr != IntPtr.Zero)
            {
                var sizeField = reflector.GetField("ArrayList", "_size", "System.Collections", "mscorlib")
                             ?? reflector.GetField("ArrayList", "size", "System.Collections", "mscorlib");
                var itemsField = reflector.GetField("ArrayList", "_items", "System.Collections", "mscorlib")
                              ?? reflector.GetField("ArrayList", "items", "System.Collections", "mscorlib");

                if (sizeField != null && itemsField != null)
                {
                    int count = reflector.Context.Memory.ReadInt(arrayListPtr + sizeField.Offset);
                    if (count > 0 && count <= 2000)
                    {
                        IntPtr itemsArrayPtr = reflector.Context.Memory.ReadIntPtr(arrayListPtr + itemsField.Offset);
                        if (itemsArrayPtr != IntPtr.Zero)
                        {
                            int ptrSize = reflector.Context.Memory.PointerSize;
                            int arrayLen = reflector.Context.Memory.ReadInt(itemsArrayPtr + ptrSize * 3);
                            int actualCount = Math.Min(count, Math.Max(0, arrayLen));
                            int elemOffset = ptrSize * 4;

                            for (int i = 0; i < actualCount; i++)
                            {
                                IntPtr elem = reflector.Context.Memory.ReadIntPtr(itemsArrayPtr + elemOffset + i * ptrSize);
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

            IntPtr sizeMethod = reflector.ResolveMethod("MyVector", "size", 0);
            IntPtr elemMethod = reflector.ResolveMethod("MyVector", "elementAt", 1);
            if (sizeMethod != IntPtr.Zero && elemMethod != IntPtr.Zero)
            {
                IntPtr sizeBoxed = reflector.InvokeMethod(sizeMethod, vectorPtr);
                int count = unchecked((int)(long)sizeBoxed);
                if (count > 0 && count <= 500)
                {
                    for (int i = 0; i < count; i++)
                    {
                        IntPtr elem = reflector.CallMethod("MyVector", "elementAt", vectorPtr, i);
                        if (elem != IntPtr.Zero)
                        {
                            result.Add(elem);
                        }
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
