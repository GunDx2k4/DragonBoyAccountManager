using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game;

public class GameTime(MonoReflector reflector)
{
    private readonly MonoReflector _reflector = reflector;

    public float TimeScale
    {
        get => GetTimeScale();
        set => SetTimeScale(value);
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
}
