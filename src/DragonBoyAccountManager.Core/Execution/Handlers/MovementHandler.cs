using DragonBoyAccountManager.Core.Automation.Navigation;
using DragonBoyAccountManager.Core.Automation.Navigation.Xmap;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class MovementHandler :
    IGameCommandHandler<MoveCommand>,
    IGameCommandHandler<NavigateCommand>,
    IGameCommandHandler<EnterWaypointCommand>,
    IGameCommandHandler<XmapMoveCommand>
{
    private readonly IGameRuntime _runtime;
    private readonly AutoNavigator? _navigator;
    private readonly ISessionContext? _sessionContext;

    public MovementHandler(
        IGameRuntime runtime,
        AutoNavigator? navigator = null,
        ISessionContext? sessionContext = null)
    {
        _runtime = runtime;
        _navigator = navigator;
        _sessionContext = sessionContext;
    }

    public async Task<Result> HandleAsync(MoveCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Result.Failure("Nhân vật chưa vào game.");

        var charLayout = _runtime.Layout.Char;
        var movePointLayout = _runtime.Layout.MovePoint;

        if (command.IsFastTeleport)
        {
            IntPtr service = _runtime.GetServicePtr();
            if (service == IntPtr.Zero)
                return Result.Failure("Không tìm thấy Service.");

            await WorldMapWaypointExecutor.TeleportToAsync(_runtime, charPtr, service, command.TargetX, command.TargetY, cancellationToken);
            return Result.Success($"Đã dịch chuyển nhanh tới ({command.TargetX}, {command.TargetY})");
        }

        IntPtr mp = _runtime.CreateInstance(GameSchema.MovePoint.ClassName, command.TargetX, command.TargetY);

        if (mp != IntPtr.Zero)
        {
            int curX = _runtime.ReadField<int>(charPtr, charLayout.X);
            _runtime.WriteField(mp, movePointLayout.XEnd, command.TargetX);
            _runtime.WriteField(mp, movePointLayout.YEnd, command.TargetY);
            _runtime.WriteField(charPtr, charLayout.CurrentMovePoint, mp);
            _runtime.WriteField(charPtr, charLayout.Cdir, command.TargetX >= curX ? 1 : -1);
            _runtime.WriteField(charPtr, charLayout.EndMovePointCommand, IntPtr.Zero);
            return Result.Success($"Đang di chuyển tới ({command.TargetX}, {command.TargetY})");
        }

        IntPtr gameScr = _runtime.GetGameScrPtr();
        if (gameScr != IntPtr.Zero)
        {
            _runtime.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodCheckClickMoveTo, gameScr, command.TargetX, command.TargetY, 0);
            return Result.Success($"Đang di chuyển tới ({command.TargetX}, {command.TargetY})");
        }

        return Result.Failure("Không thể khởi tạo MovePoint để di chuyển.");
    }

    public async Task<Result> HandleAsync(NavigateCommand command, CancellationToken cancellationToken)
    {
        if (_navigator == null || _sessionContext == null)
        {
            return await HandleAsync(new MoveCommand(command.TargetX, command.TargetY, command.IsFastTeleport), cancellationToken);
        }

        return await _navigator.NavigateAsync(
            _sessionContext.CurrentPlayer,
            _sessionContext.CurrentMap,
            command.TargetX,
            command.TargetY,
            command.IsFastTeleport,
            null,
            cancellationToken);
    }

    public async Task<Result> HandleAsync(EnterWaypointCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Result.Failure("Nhân vật chưa vào game.");

        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Result.Failure("Không tìm thấy Service.");

        int targetX = command.WaypointX;
        int targetY = command.WaypointY;
        bool isOffline = false;
        int mapPixelWidth = WorldMapWaypointExecutor.GetMapPixelWidth(_runtime, (_sessionContext?.CurrentMap.Width ?? 0) * 24);

        WorldWaypointInfo? matchedWp = null;
        if (_sessionContext != null && _sessionContext.CurrentWorld.Waypoints.Count > 0)
        {
            matchedWp = _sessionContext.CurrentWorld.Waypoints
                .OrderBy(w => Math.Abs(w.X - command.WaypointX) + Math.Abs(w.Y - command.WaypointY))
                .FirstOrDefault();

            if (matchedWp != null)
            {
                isOffline = matchedWp.IsOffline;
                targetX = WorldMapWaypointExecutor.GetWaypointTargetX(matchedWp, mapPixelWidth);
                targetY = WorldMapWaypointExecutor.GetWaypointTargetY(matchedWp);
            }
        }

        if (command.IsFastTeleport)
        {
            if (matchedWp != null)
            {
                return await WorldMapWaypointExecutor.StepToWaypointAsync(
                    _runtime,
                    charPtr,
                    service,
                    matchedWp,
                    mapPixelWidth,
                    cancellationToken);
            }

            await WorldMapWaypointExecutor.ExecuteTeleportAndChangeMapAsync(
                _runtime,
                charPtr,
                service,
                targetX,
                targetY,
                isOffline,
                cancellationToken);

            return Result.Success("Đã tele tới waypoint và gửi yêu cầu chuyển map.");
        }

        if (_navigator == null || _sessionContext == null)
        {
            return await HandleAsync(new MoveCommand(targetX, targetY, false), cancellationToken);
        }

        var navResult = await _navigator.NavigateAsync(
            _sessionContext.CurrentPlayer,
            _sessionContext.CurrentMap,
            targetX,
            targetY,
            false,
            null,
            cancellationToken);

        if (navResult.IsFailure)
            return navResult;

        await Task.Delay(80, cancellationToken);
        _sessionContext.Tick();
        if (!_sessionContext.CurrentPlayer.IsChangingMap)
        {
            string transitionMethod = isOffline ? GameSchema.Service.MethodGetMapOffline : GameSchema.Service.MethodRequestChangeMap;
            _runtime.CallMethod(GameSchema.Service.ClassName, transitionMethod, service);
        }

        return Result.Success("Đã tới waypoint.");
    }

    public async Task<Result> HandleAsync(XmapMoveCommand command, CancellationToken cancellationToken)
    {
        if (_sessionContext == null)
        {
            return Result.Failure("Phiên làm việc chưa sẵn sàng.");
        }

        return await XmapNavigator.NavigateToMapAsync(
            _sessionContext,
            command.TargetMapId,
            command.IsFastTeleport,
            null,
            cancellationToken);
    }
}
