namespace DragonBoyAccountManager.Core.Telemetry.Snapshots;

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
    IReadOnlyList<GameSkillInfo>? Skills = null,
    IReadOnlyList<InventoryItem>? BagItems = null
) : IPositioned
{
    public IReadOnlyList<GameSkillInfo> SkillList => Skills ?? [];
    public IReadOnlyList<InventoryItem> BagItemList => BagItems ?? [];

    public static PlayerSnapshot Empty => new(0, string.Empty, 0, 0, 0, 0, 0, 0, false, false, false, 0, 0, 0, false, [], []);
}
