using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Interop.Abstractions;

public static class GameRuntimeExtensions
{
    public static IntPtr GetMyCharPtr(this IGameRuntime runtime)
    {
        return runtime.ReadStatic<IntPtr>(GameSchema.Char.ClassName, runtime.Layout.Char.MyChar);
    }

    public static IntPtr GetServicePtr(this IGameRuntime runtime)
    {
        IntPtr service = runtime.ReadStatic<IntPtr>(GameSchema.Service.ClassName, runtime.Layout.Service.Instance);
        if (service == IntPtr.Zero)
        {
            service = runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
        }
        return service;
    }

    public static IntPtr GetGameScrPtr(this IGameRuntime runtime)
    {
        IntPtr gameScr = runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, runtime.Layout.GameScr.Instance);
        if (gameScr == IntPtr.Zero)
        {
            gameScr = runtime.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);
        }
        return gameScr;
    }

    public static IntPtr GetGameCanvasPtr(this IGameRuntime runtime)
    {
        return runtime.ReadStatic<IntPtr>(GameSchema.GameCanvas.ClassName, runtime.Layout.GameCanvas.Instance);
    }

    public static int ReadArrayLength(this IGameRuntime runtime, IntPtr arrayPtr)
    {
        if (arrayPtr == IntPtr.Zero) return 0;
        int lenOffset = runtime.Memory.PointerSize * 3;
        int length = runtime.ReadField<int>(arrayPtr, lenOffset);
        return length is > 0 and <= 1_000_000 ? length : 0;
    }

    public static bool ReadBoolArrayElement(this IGameRuntime runtime, IntPtr arrayPtr, int index)
    {
        int length = runtime.ReadArrayLength(arrayPtr);
        if (index < 0 || index >= length) return false;
        int dataOffset = runtime.Memory.PointerSize * 4;
        return runtime.Memory.Read<byte>(arrayPtr + dataOffset + index) != 0;
    }

    public static void WriteBoolArrayElement(this IGameRuntime runtime, IntPtr arrayPtr, int index, bool value)
    {
        int length = runtime.ReadArrayLength(arrayPtr);
        if (index < 0 || index >= length) return;
        int dataOffset = runtime.Memory.PointerSize * 4;
        runtime.Memory.Write<byte>(arrayPtr + dataOffset + index, (byte)(value ? 1 : 0));
    }

    public static string ReadStringArrayElement(this IGameRuntime runtime, IntPtr arrayPtr, int index)
    {
        int length = runtime.ReadArrayLength(arrayPtr);
        if (index < 0 || index >= length) return string.Empty;
        int ptrSize = runtime.Memory.PointerSize;
        int elemOffset = (ptrSize * 4) + (index * ptrSize);
        IntPtr elemPtr = runtime.ReadFieldPtr(arrayPtr, elemOffset);
        return runtime.ReadString(elemPtr);
    }
}
