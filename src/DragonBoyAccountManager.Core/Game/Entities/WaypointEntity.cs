using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Entities;

/// <summary>
/// Đại diện cho một điểm chuyển map (Cổng dịch chuyển / Waypoint) trên bản đồ.
/// </summary>
public sealed class WaypointEntity(MonoReflector reflector, IntPtr pointer, int pxw = 2400, string name = "Cổng sang map") 
    : WorldEntity(reflector, pointer)
{
    public override string ClassName => GameSchema.Waypoint.ClassName;

    public override int Id => (MinX << 16) | (MinY & 0xFFFF);

    public short MinX => GetField<short>(GameSchema.Waypoint.FieldMinX);
    public short MinY => GetField<short>(GameSchema.Waypoint.FieldMinY);
    public short MaxX => GetField<short>(GameSchema.Waypoint.FieldMaxX);
    public short MaxY => GetField<short>(GameSchema.Waypoint.FieldMaxY);
    public bool IsEnter => GetField<bool>(GameSchema.Waypoint.FieldIsEnter);
    public bool IsOffline => GetField<bool>(GameSchema.Waypoint.FieldIsOffline);

    public override int X => MaxX < 60 ? 15 : MinX > pxw - 60 ? pxw - 15 : MinX + ((MaxX - MinX) / 2);
    public override int Y => MaxY;

    private readonly string _defaultName = name;

    public string Name
    {
        get
        {
            try
            {
                IntPtr popupPtr = GetPtr(GameSchema.Waypoint.FieldPopup);
                if (popupPtr != IntPtr.Zero)
                {
                    IntPtr saysPtr = Reflector.ReadFieldPtr(GameSchema.PopUp.ClassName, GameSchema.PopUp.FieldSays, popupPtr);
                    if (saysPtr != IntPtr.Zero)
                    {
                        string[] lines = Reflector.ReadMonoStringArray(saysPtr);
                        if (lines != null && lines.Length > 0)
                        {
                            string combined = string.Join(" ", lines.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
                            if (!string.IsNullOrWhiteSpace(combined))
                            {
                                return combined;
                            }
                        }
                    }
                }
            }
            catch { }

            return !string.IsNullOrWhiteSpace(_defaultName) ? _defaultName : "Cổng sang map";
        }
        init => _defaultName = value;
    }

    public void Enter(int mode = 2)
    {
        Call(GameSchema.Waypoint.MethodPerform, mode, null);
    }

    public override string ToString() => $"Waypoint ({Name}) at ({X}, {Y}) [Box: ({MinX},{MinY}) -> ({MaxX},{MaxY})]";
}
