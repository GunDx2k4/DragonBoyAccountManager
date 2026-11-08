using DragonBoyAccountManager.Core.Automation.Navigation;
using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class MovementHandler :
    IGameCommandHandler<MoveCommand>,
    IGameCommandHandler<NavigateCommand>,
    IGameCommandHandler<EnterWaypointCommand>
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

    public Task<Result> HandleAsync(MoveCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        var charLayout = _runtime.Layout.Char;
        var movePointLayout = _runtime.Layout.MovePoint;

        if (command.IsFastTeleport)
        {
            _runtime.WriteField(charPtr, charLayout.CurrentMovePoint, IntPtr.Zero);
            _runtime.WriteField(charPtr, charLayout.X, command.TargetX);
            _runtime.WriteField(charPtr, charLayout.Y, command.TargetY);

            IntPtr service = _runtime.GetServicePtr();
            if (service != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, service);
            }

            return Task.FromResult(Result.Success($"Đã dịch chuyển nhanh tới ({command.TargetX}, {command.TargetY})"));
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
            return Task.FromResult(Result.Success($"Đang di chuyển tới ({command.TargetX}, {command.TargetY})"));
        }

        IntPtr gameScr = _runtime.GetGameScrPtr();
        if (gameScr != IntPtr.Zero)
        {
            _runtime.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodCheckClickMoveTo, gameScr, command.TargetX, command.TargetY, 0);
            return Task.FromResult(Result.Success($"Đang di chuyển tới ({command.TargetX}, {command.TargetY})"));
        }

        return Task.FromResult(Result.Failure("Không thể khởi tạo MovePoint để di chuyển."));
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
        if (_navigator == null || _sessionContext == null)
        {
            return await HandleAsync(new MoveCommand(command.WaypointX, command.WaypointY, command.IsFastTeleport), cancellationToken);
        }

        return await _navigator.EnterWaypointAsync(
            _sessionContext,
            command.WaypointX,
            command.WaypointY,
            command.IsFastTeleport,
            null,
            cancellationToken);
    }
}
