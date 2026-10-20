namespace DragonBoyAccountManager.Core.Domain.Snapshots;

public sealed record MobSnapshot(
    int Id,
    int TemplateId,
    string Name,
    long Hp,
    long MaxHp,
    int X,
    int Y,
    int Status,
    bool IsAlive,
    bool IsBoss,
    bool IsSuperMob = false,
    bool IsFlying = false
)
{
    public double DistanceTo(int targetX, int targetY)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public int ManhattanDistanceTo(int targetX, int targetY) =>
        Math.Abs(X - targetX) + Math.Abs(Y - targetY);
}
