using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Entities;

/// <summary>
/// Đại diện cho một thực thể sống hoặc đối tượng có tọa độ (X, Y) trên bản đồ thế giới của game.
/// Độc lập, tự quản lý con trỏ và giao tiếp bộ nhớ qua MonoReflector.
/// </summary>
public abstract class WorldEntity(MonoReflector reflector, IntPtr pointer = default)
{
    public MonoReflector Reflector { get; } = reflector;
    public virtual IntPtr Pointer { get; } = pointer;
    public virtual bool IsAlive => Pointer != IntPtr.Zero;
    public abstract string ClassName { get; }

    /// <summary>
    /// ID định danh duy nhất của thực thể trong map hiện tại.
    /// </summary>
    public abstract int Id { get; }

    /// <summary>
    /// Tọa độ X trên bản đồ thế giới (pixels).
    /// </summary>
    public abstract int X { get; }

    /// <summary>
    /// Tọa độ Y trên bản đồ thế giới (pixels).
    /// </summary>
    public abstract int Y { get; }

    protected T GetField<T>(string fieldName) where T : unmanaged
        => Reflector.ReadField<T>(ClassName, fieldName, Pointer);

    protected string GetString(string fieldName)
        => Reflector.ReadStringField(ClassName, fieldName, Pointer);

    protected IntPtr GetPtr(string fieldName)
        => Reflector.ReadFieldPtr(ClassName, fieldName, Pointer);

    protected IntPtr Call(string methodName, params object?[] args)
        => Reflector.CallMethod(ClassName, methodName, Pointer, args);

    /// <summary>
    /// Tính khoảng cách Euclidean tới điểm mục tiêu (pixels).
    /// </summary>
    public double DistanceTo(int targetX, int targetY)
    {
        int dx = X - targetX;
        int dy = Y - targetY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Tính khoảng cách Euclidean tới một thực thể khác.
    /// </summary>
    public double DistanceTo(WorldEntity other)
    {
        return other != null ? DistanceTo(other.X, other.Y) : double.MaxValue;
    }

    /// <summary>
    /// Tính khoảng cách Manhattan (dx + dy) - chuẩn tính toán nhanh trong game 2D.
    /// </summary>
    public int ManhattanDistanceTo(int targetX, int targetY)
    {
        return Math.Abs(X - targetX) + Math.Abs(Y - targetY);
    }

    public int ManhattanDistanceTo(WorldEntity other)
    {
        return other != null ? ManhattanDistanceTo(other.X, other.Y) : int.MaxValue;
    }

    /// <summary>
    /// Kiểm tra xem thực thể có đang ở trong phạm vi bán kính chỉ định hay không.
    /// </summary>
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
