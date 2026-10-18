using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Modules;

public interface IEngineModule
{
    float TimeScale { get; set; }
    (int Width, int Height) Resolution { get; }

    string Version { get; }
    int IntVersion { get; }
    string ServerIP { get; }
    int ServerPort { get; }

    bool SetTimeScale(float value);
    bool SetResolution(int width, int height, bool fullscreen = false);
}

public class EngineModule(MonoReflector reflector) : IEngineModule
{
    private readonly MonoReflector _reflector = reflector;

    public float TimeScale
    {
        get => GetTimeScale();
        set => SetTimeScale(value);
    }

    public (int Width, int Height) Resolution => GetResolution();

    public string Version
    {
        get
        {
            try { return _reflector.ReadStaticString(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticVersion) ?? string.Empty; }
            catch { return string.Empty; }
        }
    }

    public int IntVersion
    {
        get
        {
            try { return _reflector.ReadStatic<int>(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticIntVersion); }
            catch { return 0; }
        }
    }

    public string ServerIP
    {
        get
        {
            try { return _reflector.ReadStaticString(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticIp) ?? string.Empty; }
            catch { return string.Empty; }
        }
    }

    public int ServerPort
    {
        get
        {
            try { return _reflector.ReadStatic<int>(GameSchema.GameMidlet.ClassName, GameSchema.GameMidlet.StaticPort); }
            catch { return 0; }
        }
    }

    public float GetTimeScale()
    {
        try
        {
            IntPtr boxed = _reflector.CallUnityMethod("UnityEngine", "Time", "get_timeScale", IntPtr.Zero);
            if (boxed == IntPtr.Zero) return 1.0f;

            int offset = _reflector.Context.Memory.PointerSize * 2;
            return _reflector.Context.Memory.Read<float>(boxed + offset);
        }
        catch
        {
            return 1.0f;
        }
    }

    public bool SetTimeScale(float value)
    {
        try
        {
            _reflector.CallUnityMethod("UnityEngine", "Time", "set_timeScale", IntPtr.Zero, value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool SetResolution(int width, int height, bool fullscreen = false)
    {
        if (width <= 0 || height <= 0) return false;

        try
        {
            _reflector.CallUnityMethod("UnityEngine", "Screen", "SetResolution", IntPtr.Zero, width, height, fullscreen);

            try
            {
                int level = (width <= 720) ? 1 : 0;
                _reflector.CallMethod(GameSchema.Rms.ClassName, GameSchema.Rms.MethodSaveRMSInt, IntPtr.Zero, GameSchema.Rms.KeyLevelScreenKN, level);
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
