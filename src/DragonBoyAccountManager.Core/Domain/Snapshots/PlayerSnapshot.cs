namespace DragonBoyAccountManager.Core.Domain.Snapshots;

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
    long MaxMp = 0
)
{
    public static PlayerSnapshot Empty => new(0, string.Empty, 0, 0, 0, 0, 0, 0, false, false, false, 0, 0);
}
