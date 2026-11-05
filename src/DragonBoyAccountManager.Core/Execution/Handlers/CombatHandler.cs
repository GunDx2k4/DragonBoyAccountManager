using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class CombatHandler(IGameRuntime runtime) :
    IGameCommandHandler<AttackMobCommand>,
    IGameCommandHandler<SelectSkillCommand>,
    IGameCommandHandler<SetInGameAutoTrainCommand>
{
    private readonly IGameRuntime _runtime = runtime;
    private IntPtr _cachedVMob;
    private IntPtr _cachedEmptyVChar;

    public Task<Result> HandleAsync(SetInGameAutoTrainCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        _runtime.WriteStatic(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.IsAutoPlay, command.Enable);

        IntPtr autoTrainStatic = _runtime.GetStaticFieldsPtr(GameSchema.AutoTrainMenu.ClassName);
        if (autoTrainStatic != IntPtr.Zero)
        {
            if (_runtime.Layout.AutoTrainMenu.IsCheckAllMob >= 0)
            {
                _runtime.WriteField(autoTrainStatic, _runtime.Layout.AutoTrainMenu.IsCheckAllMob, true);
            }

            int isCheckedOffset = _runtime.Layout.AutoTrainMenu.IsChecked;
            if (isCheckedOffset >= 0)
            {
                IntPtr isCheckedArray = _runtime.ReadFieldPtr(autoTrainStatic, isCheckedOffset);
                if (isCheckedArray != IntPtr.Zero)
                {
                    int len = _runtime.ReadField<int>(isCheckedArray, 0x18);
                    if (len > 0)
                    {
                        _runtime.Memory.Write<byte>(isCheckedArray + 0x20, (byte)(command.Enable ? 1 : 0));
                    }
                }
            }

            if (command.Enable)
            {
                int isCheckedSkillOffset = _runtime.Layout.AutoTrainMenu.IsCheckedSkill;
                if (isCheckedSkillOffset >= 0)
                {
                    IntPtr isCheckedSkillArray = _runtime.ReadFieldPtr(autoTrainStatic, isCheckedSkillOffset);
                    if (isCheckedSkillArray != IntPtr.Zero)
                    {
                        int len = _runtime.ReadField<int>(isCheckedSkillArray, 0x18);
                        int count = Math.Clamp(len, 0, 10);
                        for (int i = 0; i < count; i++)
                        {
                            _runtime.Memory.Write<byte>(isCheckedSkillArray + 0x20 + i, 1);
                        }
                    }
                }
            }
        }

        return Task.FromResult(Result.Success(command.Enable ? "Đã bật tự đánh trong game" : "Đã tắt tự đánh trong game"));
    }

    public Task<Result> HandleAsync(SelectSkillCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        IntPtr keySkillArray = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.KeySkill);
        if (keySkillArray == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Không tìm thấy mảng kỹ năng."));

        IntPtr[] skillPtrs = _runtime.ReadPointerArray(keySkillArray);
        if (command.SlotIndex < 0 || command.SlotIndex >= skillPtrs.Length || skillPtrs[command.SlotIndex] == IntPtr.Zero)
            return Task.FromResult(Result.Failure($"Ô phím {command.SlotIndex + 1} không có kỹ năng."));

        IntPtr skillPtr = skillPtrs[command.SlotIndex];

        IntPtr currentMySkill = _runtime.ReadFieldPtr(charPtr, _runtime.Layout.Char.MySkill);
        if (currentMySkill == skillPtr)
        {
            return Task.FromResult(Result.Success($"Đã chọn kỹ năng ô {command.SlotIndex + 1}"));
        }

        _runtime.WriteField(charPtr, _runtime.Layout.Char.MySkill, skillPtr);

        IntPtr gameScr = _runtime.GetGameScrPtr();
        if (gameScr != IntPtr.Zero)
        {
            _runtime.WriteField(gameScr, _runtime.Layout.GameScr.LastSkill, skillPtr);
        }

        IntPtr templatePtr = _runtime.ReadFieldPtr(skillPtr, _runtime.Layout.Skill.Template);
        if (templatePtr != IntPtr.Zero)
        {
            sbyte templateId = _runtime.ReadField<sbyte>(templatePtr, _runtime.Layout.SkillTemplate.Id);
            IntPtr service = _runtime.GetServicePtr();
            if (service != IntPtr.Zero)
            {
                _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSelectSkill, service, (int)templateId);
            }
        }

        return Task.FromResult(Result.Success($"Đã chọn kỹ năng ô {command.SlotIndex + 1}"));
    }

    public async Task<Result> HandleAsync(AttackMobCommand command, CancellationToken cancellationToken)
    {
        IntPtr mobPtr = IntPtr.Zero;
        IntPtr vMobPtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.VMob);
        if (vMobPtr != IntPtr.Zero)
        {
            var pointers = CollectionHelper.ReadMyVector(_runtime, vMobPtr);
            foreach (var ptr in pointers)
            {
                if (ptr != IntPtr.Zero && _runtime.ReadField<int>(ptr, _runtime.Layout.Mob.MobId) == command.MobId)
                {
                    mobPtr = ptr;
                    break;
                }
            }
        }

        if (mobPtr == IntPtr.Zero)
            return Result.Failure($"Không tìm thấy quái #{command.MobId} hoặc con trỏ không hợp lệ.");

        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Result.Failure("Nhân vật chưa vào game.");

        int playerX = _runtime.ReadField<int>(charPtr, _runtime.Layout.Char.X);
        int cdir = command.TargetX >= playerX ? 1 : -1;
        _runtime.WriteField(charPtr, _runtime.Layout.Char.Cdir, cdir);

        if (command.SkillSlot >= 0)
        {
            await HandleAsync(new SelectSkillCommand(command.SkillSlot), cancellationToken);
        }

        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Result.Failure("Không tìm thấy Service.");

        IntPtr vMob = EnsureValidMyVector(ref _cachedVMob, clearElements: true);
        IntPtr vChar = EnsureValidMyVector(ref _cachedEmptyVChar, clearElements: false);

        if (vMob == IntPtr.Zero || vChar == IntPtr.Zero)
            return Result.Failure("Không thể tạo vector tấn công.");

        _runtime.CallMethod(GameSchema.MyVector.ClassName, GameSchema.MyVector.MethodAddElement, vMob, mobPtr);

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSendPlayerAttack, service, vMob, vChar, 1);

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        IntPtr mySkillPtr = _runtime.ReadFieldPtr(charPtr, _runtime.Layout.Char.MySkill);
        if (mySkillPtr != IntPtr.Zero)
        {
            _runtime.WriteField(mySkillPtr, _runtime.Layout.Skill.LastTimeUseThisSkill, nowMs);
        }

        return Result.Success($"Đã đánh {command.MobName} #{command.MobId}");
    }

    private IntPtr EnsureValidMyVector(ref IntPtr vectorPtr, bool clearElements)
    {
        if (vectorPtr != IntPtr.Zero &&
            _runtime.GetClassName(vectorPtr) == GameSchema.MyVector.ClassName &&
            _runtime.ReadFieldPtr(vectorPtr, _runtime.Layout.MyVector.A) != IntPtr.Zero)
        {
            if (clearElements)
            {
                _runtime.CallMethod(GameSchema.MyVector.ClassName, GameSchema.MyVector.MethodRemoveAllElements, vectorPtr);
            }
            return vectorPtr;
        }

        vectorPtr = _runtime.CreateInstance(GameSchema.MyVector.ClassName);
        return vectorPtr;
    }
}
