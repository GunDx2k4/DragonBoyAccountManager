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

    public string Name { get; init; } = name;

    public void Enter(int mode = 2)
    {
        Call(GameSchema.Waypoint.MethodPerform, mode, null);
    }

    public override string ToString() => $"Waypoint ({Name}) at ({X}, {Y}) [Box: ({MinX},{MinY}) -> ({MaxX},{MaxY})]";
}
