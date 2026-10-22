using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class MovementHandler(IMonoRuntime mono) : IGameCommandHandler<MoveCommand>
{
    private readonly IMonoRuntime _mono = mono;

    public Task<Result> HandleAsync(MoveCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _mono.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        if (command.IsFastTeleport)
        {
            _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, charPtr, IntPtr.Zero);
            _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, charPtr, command.TargetX);
            _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, charPtr, command.TargetY);

            IntPtr service = _mono.GetServicePtr();
            if (service != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodCharMove, service);
            }

            return Task.FromResult(Result.Success($"Đã dịch chuyển nhanh tới ({command.TargetX}, {command.TargetY})"));
        }

        IntPtr mp = _mono.CreateInstance(GameSchema.MovePoint.ClassName, command.TargetX, command.TargetY);
        if (mp != IntPtr.Zero)
        {
            int curX = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, charPtr);
            _mono.WriteField(GameSchema.MovePoint.ClassName, GameSchema.MovePoint.FieldXEnd, mp, command.TargetX);
            _mono.WriteField(GameSchema.MovePoint.ClassName, GameSchema.MovePoint.FieldYEnd, mp, command.TargetY);
            _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, charPtr, mp);
            _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCdir, charPtr, command.TargetX >= curX ? 1 : -1);
            _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldEndMovePointCommand, charPtr, IntPtr.Zero);
            return Task.FromResult(Result.Success($"Đang di chuyển tới ({command.TargetX}, {command.TargetY})"));
        }

        IntPtr gameScr = _mono.GetGameScrPtr();
        if (gameScr != IntPtr.Zero)
        {
            _mono.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodCheckClickMoveTo, gameScr, command.TargetX, command.TargetY, 0);
            return Task.FromResult(Result.Success($"Đang di chuyển tới ({command.TargetX}, {command.TargetY})"));
        }

        return Task.FromResult(Result.Failure("Không thể khởi tạo MovePoint để di chuyển."));
    }
}
