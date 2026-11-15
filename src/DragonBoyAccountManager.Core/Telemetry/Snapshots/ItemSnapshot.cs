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
    long LastTimeUse = 0,
    sbyte TemplateId = -1,
    int ManaUseType = 0,
    int SkillType = 1,
    int Dx = 0,
    int Dy = 0
)
{
    public bool IsReady(long nowMs) => nowMs - LastTimeUse >= Math.Max(500, CoolDown);

    public bool IsSelfOrBuffSkill => SkillType == 2 || (SkillType == 3 && TemplateId is 6 or 8 or 12 or 13 or 14 or 19 or 21);

    public bool HasEnoughMana(long currentMp, long maxMp)
    {
        if (ManaUseType == 2)
        {
            return currentMp >= 1;
        }

        if (ManaUseType == 1)
        {
            return maxMp > 0 && currentMp >= maxMp * ManaUse / 100L;
        }

        return currentMp >= ManaUse;
    }
}
