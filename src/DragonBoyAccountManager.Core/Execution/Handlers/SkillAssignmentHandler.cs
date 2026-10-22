using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class SkillAssignmentHandler(IMonoRuntime mono) : IGameCommandHandler<AssignSkillsCommand>
{
    private readonly IMonoRuntime _mono = mono;

    public Task<Result> HandleAsync(AssignSkillsCommand command, CancellationToken cancellationToken)
    {
        try
        {
            IntPtr charPtr = _mono.GetMyCharPtr();
            if (charPtr == IntPtr.Zero)
                return Task.FromResult(Result.Failure("Không tìm thấy con trỏ nhân vật."));

            IntPtr vSkillFightPtr = _mono.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldVSkillFight, charPtr);
            if (vSkillFightPtr == IntPtr.Zero)
                return Task.FromResult(Result.Failure("Không tìm thấy danh sách kỹ năng chiến đấu."));

            var vSkills = CollectionHelper.ReadMyVector(_mono, vSkillFightPtr);
            var validSkills = vSkills.Where(s => s != IntPtr.Zero).ToList();
            if (validSkills.Count == 0)
                return Task.FromResult(Result.Failure("Nhân vật không có kỹ năng nào để gán."));

            IntPtr keySkillArray = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill);
            IntPtr onScreenSkillArray = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticOnScreenSkill);

            int assignedCount = Math.Min(validSkills.Count, 10);
            for (int i = 0; i < 10; i++)
            {
                IntPtr skillPtr = i < assignedCount ? validSkills[i] : IntPtr.Zero;
                if (keySkillArray != IntPtr.Zero)
                {
                    _mono.WriteMonoPointerArrayElement(keySkillArray, i, skillPtr);
                }
                if (onScreenSkillArray != IntPtr.Zero)
                {
                    _mono.WriteMonoPointerArrayElement(onScreenSkillArray, i, skillPtr);
                }
            }

            IntPtr mySkillPtr = _mono.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, charPtr);
            if (mySkillPtr == IntPtr.Zero && validSkills.Count > 0)
            {
                _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, charPtr, validSkills[0]);
            }

            IntPtr gameScr = _mono.GetGameScrPtr();
            if (gameScr != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodSaveKeySkillToRMS, gameScr);
            }

            return Task.FromResult(Result.Success($"Đã tự động gán {assignedCount} kỹ năng vào phím tắt 1 - 0 thành công!"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result.Failure($"Lỗi khi gán kỹ năng: {ex.Message}", ex));
        }
    }
}
