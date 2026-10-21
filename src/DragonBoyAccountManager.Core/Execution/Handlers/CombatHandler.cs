using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class CombatHandler(IMonoRuntime mono) :
    IGameCommandHandler<AttackMobCommand>,
    IGameCommandHandler<SelectSkillCommand>
{
    private readonly IMonoRuntime _mono = mono;

    private IntPtr GetServicePointer()
    {
        IntPtr service = _mono.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
        if (service == IntPtr.Zero)
            service = _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
        return service;
    }

    private IntPtr GetMyCharPtr()
    {
        return _mono.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
    }

    public Task<Result> HandleAsync(SelectSkillCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        IntPtr keySkillArray = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill);
        if (keySkillArray == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy mảng kỹ năng."));

        IntPtr[] skillPtrs = _mono.ReadMonoPointerArray(keySkillArray);
        if (command.SlotIndex < 0 || command.SlotIndex >= skillPtrs.Length || skillPtrs[command.SlotIndex] == IntPtr.Zero)
            return Task.FromResult(Result.Failure($"Ô phím {command.SlotIndex + 1} không có kỹ năng."));

        IntPtr skillPtr = skillPtrs[command.SlotIndex];

        _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, charPtr, skillPtr);

        IntPtr gameScr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
        if (gameScr == IntPtr.Zero)
            gameScr = _mono.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);

        if (gameScr != IntPtr.Zero)
        {
            _mono.WriteField(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldLastSkill, gameScr, skillPtr);
        }

        IntPtr templatePtr = _mono.ReadFieldPtr(GameSchema.Skill.ClassName, GameSchema.Skill.FieldTemplate, skillPtr);
        if (templatePtr != IntPtr.Zero)
        {
            sbyte templateId = _mono.ReadField<sbyte>(GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldId, templatePtr);
            IntPtr service = GetServicePointer();
            if (service != IntPtr.Zero)
            {
                _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSelectSkill, service, (int)templateId);
            }
        }

        return Task.FromResult(Result.Success($"Đã chọn kỹ năng ô {command.SlotIndex + 1}"));
    }

    public Task<Result> HandleAsync(AttackMobCommand command, CancellationToken cancellationToken)
    {
        IntPtr mobPtr = command.MobPointer;
        if (mobPtr == IntPtr.Zero)
        {
            IntPtr vMobPtr = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticVMob);
            if (vMobPtr != IntPtr.Zero)
            {
                var pointers = CollectionHelper.ReadMyVector(_mono, vMobPtr);
                foreach (var ptr in pointers)
                {
                    if (ptr != IntPtr.Zero && _mono.ReadField<int>(GameSchema.Mob.ClassName, GameSchema.Mob.FieldMobId, ptr) == command.MobId)
                    {
                        mobPtr = ptr;
                        break;
                    }
                }
            }
        }

        if (mobPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure($"Không tìm thấy quái #{command.MobId} hoặc con trỏ không hợp lệ."));

        IntPtr charPtr = GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        int playerX = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, charPtr);
        int cdir = command.TargetX >= playerX ? 1 : -1;
        _mono.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCdir, charPtr, cdir);

        if (command.SkillSlot >= 0)
        {
            _ = HandleAsync(new SelectSkillCommand(command.SkillSlot), cancellationToken);
        }

        IntPtr service = GetServicePointer();
        if (service == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy Service."));

        _mono.WriteStatic(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticIsAutoPlay, false);
        IntPtr gs = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
        if (gs != IntPtr.Zero)
        {
            _mono.WriteField(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldAuto, gs, 0);
        }

        IntPtr vMob = _mono.CreateInstance(GameSchema.MyVector.ClassName);
        IntPtr vChar = _mono.CreateInstance(GameSchema.MyVector.ClassName);

        if (vMob == IntPtr.Zero || vChar == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không thể tạo vector tấn công."));

        _mono.CallMethod(GameSchema.MyVector.ClassName, GameSchema.MyVector.MethodAddElement, vMob, mobPtr);

        _mono.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSendPlayerAttack, service, vMob, vChar, 1);

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        IntPtr mySkillPtr = _mono.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, charPtr);
        if (mySkillPtr != IntPtr.Zero)
        {
            _mono.WriteField(GameSchema.Skill.ClassName, GameSchema.Skill.FieldLastTimeUseThisSkill, mySkillPtr, nowMs);
        }

        return Task.FromResult(Result.Success($"Đã đánh {command.MobName} #{command.MobId}"));
    }
}
