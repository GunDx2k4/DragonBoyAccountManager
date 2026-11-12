namespace DragonBoyAccountManager.Core.Execution.Commands;

public sealed record MoveCommand(
    int TargetX,
    int TargetY,
    bool IsFastTeleport = false
) : IGameCommand
{
    public string Name => $"Di chuyển tới ({TargetX}, {TargetY})";
}

public sealed record NavigateCommand(
    int TargetX,
    int TargetY,
    bool IsFastTeleport = false
) : IGameCommand
{
    public string Name => $"Tự tìm đường tới ({TargetX}, {TargetY})";
}

public sealed record EnterWaypointCommand(
    int WaypointX,
    int WaypointY,
    bool IsFastTeleport = false
) : IGameCommand
{
    public string Name => $"Đi qua waypoint ({WaypointX}, {WaypointY})";
}

public sealed record XmapMoveCommand(
    int TargetMapId,
    bool IsFastTeleport = false
) : IGameCommand
{
    public string Name => $"Xmap tới map #{TargetMapId}";
}
