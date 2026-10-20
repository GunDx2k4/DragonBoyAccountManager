
namespace DragonBoyAccountManager.Core.Domain.Entities;

public abstract class WorldEntity(MonoReflector reflector, IntPtr pointer = default)
{
    public MonoReflector Reflector { get; } = reflector;
    public virtual IntPtr Pointer { get; } = pointer;
    public virtual bool IsAlive => Pointer != IntPtr.Zero;
    public abstract string ClassName { get; }

    public abstract int Id { get; }

    public abstract int X { get; }

    public abstract int Y { get; }

    protected T GetField<T>(string fieldName) where T : unmanaged
        => Reflector.ReadField<T>(ClassName, fieldName, Pointer);

    protected string GetString(string fieldName)
        => Reflector.ReadStringField(ClassName, fieldName, Pointer);

    protected IntPtr GetPtr(string fieldName)
        => Reflector.ReadFieldPtr(ClassName, fieldName, Pointer);

    protected IntPtr Call(string methodName, params object?[] args)
        => Reflector.CallMethod(ClassName, methodName, Pointer, args);

    public double DistanceTo(int targetX, int targetY)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public double DistanceTo(WorldEntity other)
    {
        return other != null ? DistanceTo(other.X, other.Y) : double.MaxValue;
    }

    public int ManhattanDistanceTo(int targetX, int targetY)
    {
        return Math.Abs(X - targetX) + Math.Abs(Y - targetY);
    }

    public int ManhattanDistanceTo(WorldEntity other)
    {
        return other != null ? ManhattanDistanceTo(other.X, other.Y) : int.MaxValue;
    }

    public bool IsInRange(int targetX, int targetY, int radius)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return (dx * dx + dy * dy) <= (radius * radius);
    }

    public bool IsInRange(WorldEntity other, int radius)
    {
        return other != null && IsInRange(other.X, other.Y, radius);
    }

    public override string ToString() => $"{ClassName} [Id={Id}, Pos=({X},{Y})]";
}
