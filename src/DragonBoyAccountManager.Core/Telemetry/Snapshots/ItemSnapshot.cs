namespace DragonBoyAccountManager.Core.Telemetry.Snapshots;

public sealed record InventoryItem(
    sbyte Index,
    short TemplateId,
    string Name,
    string Description,
    int Quantity,
    int Type,
    bool IsPean = false,
    bool IsCapsule = false,
    bool IsTdlt = false
);

public sealed record GroundItemSnapshot(
    short ItemMapId,
    short TemplateId,
    string Name,
    int X,
    int Y,
    int PlayerId
)
{
    public double DistanceTo(int targetX, int targetY)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public sealed record GameSkillInfo(
    int Slot,
    short SkillId,
    string Name,
    int Point,
    int CoolDown,
    int ManaUse,
    int IconId = -1,
    bool IsSelected = false,
    long LastTimeUse = 0
)
{
    public bool IsReady(long nowMs) => nowMs - LastTimeUse >= CoolDown;
}
