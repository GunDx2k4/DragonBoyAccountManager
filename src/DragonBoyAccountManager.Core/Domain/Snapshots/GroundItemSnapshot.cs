namespace DragonBoyAccountManager.Core.Domain.Snapshots;

public sealed record GroundItemSnapshot(
    int Id,
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
