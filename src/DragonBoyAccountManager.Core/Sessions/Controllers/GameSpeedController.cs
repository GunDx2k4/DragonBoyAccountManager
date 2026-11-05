using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;

namespace DragonBoyAccountManager.Core.Sessions.Controllers;

public sealed class GameSpeedController(IGameRuntime runtime)
{
    private readonly IGameRuntime _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    private volatile bool _isApplied = true;

    public float TargetSpeed { get; private set; } = 1.0f;

    public void SetSpeed(float speed, bool isInGame)
    {
        TargetSpeed = speed;
        if (Math.Abs(speed - 1.0f) < 0.001f && _isApplied)
        {
            _isApplied = true;
            return;
        }

        _isApplied = TryApplySpeed(speed, isInGame);
    }

    public void OnTick(bool isInGame)
    {
        if (!_isApplied)
        {
            _isApplied = TryApplySpeed(TargetSpeed, isInGame);
        }
    }

    public bool TryApplySpeed(float speed, bool isInGame)
    {
        try
        {
            int tick = 0;
            bool started = false;
            try
            {
                tick = _runtime.ReadStatic<int>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.GameTick);
                started = _runtime.ReadStatic<bool>(GameSchema.Main.ClassName, _runtime.Layout.Main.Started);
            }
            catch
            {
            }

            if (started || tick > 0 || isInGame)
            {
                _runtime.CallMethod(GameSchema.Time.ClassName, GameSchema.Time.MethodSetTimeScale, IntPtr.Zero, speed);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}
