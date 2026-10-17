namespace DragonBoyAccountManager.Core.Game.Snapshots;

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
    bool IsChangingMap = false
)
{
    public static PlayerSnapshot Empty => new(0, string.Empty, 0, 0, 0, 0, 0, 0, false, false, false);
}
