using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class SkillAssignmentHandler(IGameRuntime runtime) : IGameCommandHandler<AssignSkillsCommand>
{
    private readonly IGameRuntime _runtime = runtime;

    public Task<Result> HandleAsync(AssignSkillsCommand command, CancellationToken cancellationToken)
    {
        try
        {
            IntPtr charPtr = _runtime.GetMyCharPtr();
            if (charPtr == IntPtr.Zero)
                return Task.FromResult(Result.Failure("Không tìm thấy con trỏ nhân vật."));

            var charLayout = _runtime.Layout.Char;
            IntPtr vSkillFightPtr = _runtime.ReadFieldPtr(charPtr, charLayout.VSkillFight);
            if (vSkillFightPtr == IntPtr.Zero)
                return Task.FromResult(Result.Failure("Không tìm thấy danh sách kỹ năng chiến đấu."));

            var vSkills = CollectionHelper.ReadMyVector(_runtime, vSkillFightPtr);
            var validSkills = vSkills.Where(s => s != IntPtr.Zero).ToList();
            if (validSkills.Count == 0)
                return Task.FromResult(Result.Failure("Nhân vật không có kỹ năng nào để gán."));

            IntPtr keySkillArray = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.KeySkill);
            IntPtr onScreenSkillArray = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.OnScreenSkill);

            int assignedCount = Math.Min(validSkills.Count, 10);
            for (int i = 0; i < 10; i++)
            {
                IntPtr skillPtr = i < assignedCount ? validSkills[i] : IntPtr.Zero;
                if (keySkillArray != IntPtr.Zero)
                {
                    _runtime.WritePointerArrayElement(keySkillArray, i, skillPtr);
                }
                if (onScreenSkillArray != IntPtr.Zero)
                {
                    _runtime.WritePointerArrayElement(onScreenSkillArray, i, skillPtr);
                }
            }

            IntPtr mySkillPtr = _runtime.ReadFieldPtr(charPtr, charLayout.MySkill);
            if (mySkillPtr == IntPtr.Zero && validSkills.Count > 0)
            {
                _runtime.WriteField(charPtr, charLayout.MySkill, validSkills[0]);
            }

            IntPtr gameScr = _runtime.GetGameScrPtr();
            if (gameScr != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodSaveKeySkillToRMS, gameScr);
            }

            return Task.FromResult(Result.Success($"Đã tự động gán {assignedCount} kỹ năng vào phím tắt 1 - 0 thành công!"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure($"Lỗi khi gán kỹ năng: {ex.Message}"));
        }
    }
}
