using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameResolution(MonoReflector reflector)
{
    private readonly MonoReflector _reflector = reflector;

    public bool SetResolution(int width, int height, bool fullscreen = false)
    {
        if (width <= 0 || height <= 0) return false;

        try
        {
            // 1. Gọi trực tiếp Screen.SetResolution của Unity để engine resize viewport & swapchain
            _reflector.CallUnityMethod("UnityEngine", "Screen", "SetResolution", IntPtr.Zero, width, height, fullscreen);

            // 2. Đồng bộ giá trị levelScreenKN vào RMS của Dragon Boy
            // levelScreenKN = 1 tương ứng với độ phân giải 720x320 (zoomLevel x1)
            // levelScreenKN = 0 tương ứng với độ phân giải 1024x600 (zoomLevel x2)
            try
            {
                int level = (width <= 720) ? 1 : 0;
                _reflector.CallMethod("Rms", "saveRMSInt", IntPtr.Zero, "levelScreenKN", level);
            }
            catch { }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public (int Width, int Height) GetResolution()
    {
        try
        {
            IntPtr wBoxed = _reflector.CallUnityMethod("UnityEngine", "Screen", "get_width", IntPtr.Zero);
            IntPtr hBoxed = _reflector.CallUnityMethod("UnityEngine", "Screen", "get_height", IntPtr.Zero);

            int ptrSize = _reflector.Context.Memory.PointerSize;
            int w = wBoxed != IntPtr.Zero ? _reflector.Context.Memory.Read<int>(wBoxed + ptrSize * 2) : 0;
            int h = hBoxed != IntPtr.Zero ? _reflector.Context.Memory.Read<int>(hBoxed + ptrSize * 2) : 0;
            return (w, h);
        }
        catch
        {
            return (0, 0);
        }
    }
}
