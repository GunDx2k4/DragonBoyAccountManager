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

public sealed record PlayerSnapshot(
    int Id,
    string Name,
    long Hp,
    long MaxHp,
    long Power,
    int Gender,
    int X,
    int Y,
    bool IsAlive,
    bool IsInGame,
    bool IsChangingMap = false,
    long Mp = 0,
    long MaxMp = 0,
    int StatusMe = 0,
    bool IsTdltActive = false,
    bool IsAutoPlay = false,
    IReadOnlyList<GameSkillInfo>? Skills = null,
    IReadOnlyList<InventoryItem>? BagItems = null,
    IReadOnlyList<int>? ActiveBuffIconIds = null
) : IPositioned
{
    public IReadOnlyList<GameSkillInfo> SkillList => Skills ?? [];
    public IReadOnlyList<InventoryItem> BagItemList => BagItems ?? [];
    public IReadOnlyList<int> ActiveBuffIconIdList => ActiveBuffIconIds ?? [];

    public static PlayerSnapshot Empty => new(0, string.Empty, 0, 0, 0, 0, 0, 0, false, false, false, 0, 0, 0, false, false, [], [], []);
}
