using System;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameResolution
{
    private readonly MonoReflector _reflector;
    private readonly MonoContext _context;
    private IntPtr _screenClass = IntPtr.Zero;
    private IntPtr _setResolutionMethod = IntPtr.Zero;
    private IntPtr _getWidthMethod = IntPtr.Zero;
    private IntPtr _getHeightMethod = IntPtr.Zero;

    public GameResolution(MonoReflector reflector)
    {
        _reflector = reflector;
        _context = reflector.Context;
    }

    private bool EnsureInitialized()
    {
        if (_screenClass != IntPtr.Zero && _setResolutionMethod != IntPtr.Zero)
            return true;

        _screenClass = _reflector.GetClass("Screen", "UnityEngine", "UnityEngine");
        if (_screenClass == IntPtr.Zero)
            _screenClass = _reflector.GetClass("Screen", "UnityEngine", "UnityEngine.dll");
        if (_screenClass == IntPtr.Zero)
            _screenClass = _reflector.GetClass("Screen", "UnityEngine", "UnityEngine.CoreModule");
        if (_screenClass == IntPtr.Zero)
            _screenClass = _reflector.GetClass("Screen", "UnityEngine", "UnityEngine.CoreModule.dll");

        if (_screenClass != IntPtr.Zero)
        {
            _setResolutionMethod = _reflector.ResolveMethod("Screen", "SetResolution", 3, "UnityEngine", "UnityEngine");
            if (_setResolutionMethod == IntPtr.Zero)
                _setResolutionMethod = _reflector.ResolveMethod("Screen", "SetResolution", 3, "UnityEngine", "UnityEngine.CoreModule");

            _getWidthMethod = _reflector.ResolveMethod("Screen", "get_width", 0, "UnityEngine", "UnityEngine");
            if (_getWidthMethod == IntPtr.Zero)
                _getWidthMethod = _reflector.ResolveMethod("Screen", "get_width", 0, "UnityEngine", "UnityEngine.CoreModule");

            _getHeightMethod = _reflector.ResolveMethod("Screen", "get_height", 0, "UnityEngine", "UnityEngine");
            if (_getHeightMethod == IntPtr.Zero)
                _getHeightMethod = _reflector.ResolveMethod("Screen", "get_height", 0, "UnityEngine", "UnityEngine.CoreModule");
        }

        return _screenClass != IntPtr.Zero && _setResolutionMethod != IntPtr.Zero;
    }

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
        if (!EnsureInitialized() || _getWidthMethod == IntPtr.Zero || _getHeightMethod == IntPtr.Zero)
            return (0, 0);

        try
        {
            IntPtr wBoxed = _reflector.InvokeMethod(_getWidthMethod, IntPtr.Zero);
            IntPtr hBoxed = _reflector.InvokeMethod(_getHeightMethod, IntPtr.Zero);

            int ptrSize = _context.Memory.PointerSize;
            int w = wBoxed != IntPtr.Zero ? _context.Memory.Read<int>(wBoxed + ptrSize * 2) : 0;
            int h = hBoxed != IntPtr.Zero ? _context.Memory.Read<int>(hBoxed + ptrSize * 2) : 0;
            return (w, h);
        }
        catch
        {
            return (0, 0);
        }
    }
}
