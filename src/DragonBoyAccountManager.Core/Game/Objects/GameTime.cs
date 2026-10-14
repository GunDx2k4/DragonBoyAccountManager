using System;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameTime
{
    private readonly MonoReflector _reflector;
    private readonly MonoContext _context;
    private IntPtr _timeClass = IntPtr.Zero;
    private IntPtr _getTimeScaleMethod = IntPtr.Zero;
    private IntPtr _setTimeScaleMethod = IntPtr.Zero;

    public GameTime(MonoReflector reflector)
    {
        _reflector = reflector;
        _context = reflector.Context;
    }

    private bool EnsureInitialized()
    {
        if (_timeClass != IntPtr.Zero && _setTimeScaleMethod != IntPtr.Zero)
            return true;

        _timeClass = _reflector.GetClass("Time", "UnityEngine", "UnityEngine");
        if (_timeClass == IntPtr.Zero)
            _timeClass = _reflector.GetClass("Time", "UnityEngine", "UnityEngine.dll");
        if (_timeClass == IntPtr.Zero)
            _timeClass = _reflector.GetClass("Time", "UnityEngine", "UnityEngine.CoreModule");
        if (_timeClass == IntPtr.Zero)
            _timeClass = _reflector.GetClass("Time", "UnityEngine", "UnityEngine.CoreModule.dll");

        if (_timeClass != IntPtr.Zero)
        {
            _getTimeScaleMethod = _reflector.ResolveMethod("Time", "get_timeScale", 0, "UnityEngine", "UnityEngine");
            if (_getTimeScaleMethod == IntPtr.Zero)
                _getTimeScaleMethod = _reflector.ResolveMethod("Time", "get_timeScale", 0, "UnityEngine", "UnityEngine.CoreModule");

            _setTimeScaleMethod = _reflector.ResolveMethod("Time", "set_timeScale", 1, "UnityEngine", "UnityEngine");
            if (_setTimeScaleMethod == IntPtr.Zero)
                _setTimeScaleMethod = _reflector.ResolveMethod("Time", "set_timeScale", 1, "UnityEngine", "UnityEngine.CoreModule");
        }

        return _timeClass != IntPtr.Zero && _setTimeScaleMethod != IntPtr.Zero;
    }

    public float TimeScale
    {
        get => GetTimeScale();
        set => SetTimeScale(value);
    }

    public float GetTimeScale()
    {
        if (!EnsureInitialized() || _getTimeScaleMethod == IntPtr.Zero)
            return 1.0f;

        try
        {
            IntPtr boxedObj = _reflector.InvokeMethod(_getTimeScaleMethod, IntPtr.Zero);
            if (boxedObj == IntPtr.Zero) return 1.0f;

            int offset = _context.Memory.PointerSize * 2;
            return _context.Memory.Read<float>(boxedObj + offset);
        }
        catch
        {
            return 1.0f;
        }
    }

    public bool SetTimeScale(float value)
    {
        if (!EnsureInitialized() || _setTimeScaleMethod == IntPtr.Zero)
            return false;

        try
        {
            IntPtr valPtr = _context.Memory.AllocateAndWrite(value);
            try
            {
                IntPtr result = _reflector.InvokeMethod(_setTimeScaleMethod, IntPtr.Zero, valPtr);
                return true;
            }
            finally
            {
                _context.Memory.Free(valPtr);
            }
        }
        catch
        {
            return false;
        }
    }
}
