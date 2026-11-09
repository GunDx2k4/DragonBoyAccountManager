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
    private IntPtr _cachedVMobItems;
    private IntPtr _cachedEmptyVChar;

    public Task<Result> HandleAsync(SetInGameAutoTrainCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Task.FromResult(Result.Failure("Nhân vật chưa vào game."));

        bool isSaveConfigOnly = string.Equals(command.Reason, "DBAM_SaveConfig", StringComparison.Ordinal);
        bool shouldSyncConfig = command.Enable
            || string.Equals(command.Reason, "DBAM_Takeover", StringComparison.Ordinal)
            || isSaveConfigOnly;

        if (!isSaveConfigOnly)
        {
            _runtime.WriteStatic(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.IsAutoPlay, command.Enable);
        }

        IntPtr autoTrainStatic = _runtime.GetStaticFieldsPtr(GameSchema.AutoTrainMenu.ClassName);
        if (autoTrainStatic != IntPtr.Zero)
        {
            var layout = _runtime.Layout.AutoTrainMenu;

            if (layout.IsCheckItem >= 0)
            {
                IntPtr isCheckItemArray = _runtime.ReadFieldPtr(autoTrainStatic, layout.IsCheckItem);
                if (isCheckItemArray != IntPtr.Zero)
                {
                    var selectedItemSet = command.SelectedItemTemplateIds is { Count: > 0 }
                        ? command.SelectedItemTemplateIds.ToHashSet()
                        : [];
                    foreach (var def in GameDefinitions.AutoTrainItems)
                    {
                        _runtime.WriteBoolArrayElement(isCheckItemArray, def.Index, selectedItemSet.Contains(def.TemplateId));
                    }
                }
            }

            if (layout.IsChecked >= 0)
            {
                IntPtr isCheckedArray = _runtime.ReadFieldPtr(autoTrainStatic, layout.IsChecked);
                if (isCheckedArray != IntPtr.Zero)
                {
                    if (!isSaveConfigOnly)
                    {
                        _runtime.WriteBoolArrayElement(isCheckedArray, 0, command.Enable);
                    }
                    if (shouldSyncConfig)
                    {
                        _runtime.WriteBoolArrayElement(isCheckedArray, 1, command.AvoidSuperMobs);
                        _runtime.WriteBoolArrayElement(isCheckedArray, 2, command.ChangeZoneWhenNoMobs);
                    }
                }
            }

            if (!shouldSyncConfig)
            {
                return Task.FromResult(Result.Success("Đã tắt tự đánh trong game"));
            }

            if (layout.IsCheckAttackByHp >= 0)
            {
                _runtime.WriteField(autoTrainStatic, layout.IsCheckAttackByHp, command.CheckAttackByHp);
            }
            if (layout.HpQuaiOn >= 0)
            {
                _runtime.WriteField(autoTrainStatic, layout.HpQuaiOn, command.MinMobHp);
            }
            if (layout.HpQuaiDuoi >= 0)
            {
                _runtime.WriteField(autoTrainStatic, layout.HpQuaiDuoi, command.MaxMobHp);
            }

            if (layout.IsCheckAutoPean >= 0)
            {
                _runtime.WriteField(autoTrainStatic, layout.IsCheckAutoPean, command.AutoUsePean);
            }
            if (layout.PeanHpPercent >= 0)
            {
                _runtime.WriteField(autoTrainStatic, layout.PeanHpPercent, command.PeanHpPercent);
            }
            if (layout.PeanMpPercent >= 0)
            {
                _runtime.WriteField(autoTrainStatic, layout.PeanMpPercent, command.PeanMpPercent);
            }

            if (layout.IsCheckAllMob >= 0)
            {
                _runtime.WriteField(autoTrainStatic, layout.IsCheckAllMob, command.CheckAllMobs);
            }

            if (!command.CheckAllMobs && command.TargetMobNames != null && layout.ListMobNames >= 0 && layout.IsCheckedMobName >= 0)
            {
                IntPtr listMobNamesPtr = _runtime.ReadFieldPtr(autoTrainStatic, layout.ListMobNames);
                IntPtr isCheckedMobNameArray = _runtime.ReadFieldPtr(autoTrainStatic, layout.IsCheckedMobName);
                if (listMobNamesPtr != IntPtr.Zero && isCheckedMobNameArray != IntPtr.Zero)
                {
                    var mobNamePointers = CollectionHelper.ReadMyVector(_runtime, listMobNamesPtr);
                    int arrayLen = _runtime.ReadArrayLength(isCheckedMobNameArray);
                    int count = Math.Min(mobNamePointers.Count, arrayLen);
                    var targetSet = command.TargetMobNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < count; i++)
                    {
                        string name = _runtime.ReadString(mobNamePointers[i]);
                        _runtime.WriteBoolArrayElement(isCheckedMobNameArray, i, targetSet.Contains(name));
                    }
                }
            }

            if (layout.IsCheckedSkill >= 0)
            {
                IntPtr isCheckedSkillArray = _runtime.ReadFieldPtr(autoTrainStatic, layout.IsCheckedSkill);
                if (isCheckedSkillArray != IntPtr.Zero)
                {
                    int arrayLen = _runtime.ReadArrayLength(isCheckedSkillArray);
                    IntPtr listSkillsPtr = layout.ListSkills >= 0 ? _runtime.ReadFieldPtr(autoTrainStatic, layout.ListSkills) : IntPtr.Zero;
                    var skillPointers = listSkillsPtr != IntPtr.Zero ? CollectionHelper.ReadMyVector(_runtime, listSkillsPtr) : [];

                    IntPtr keySkillArray = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.KeySkill);
                    var keySkillPtrs = keySkillArray != IntPtr.Zero ? _runtime.ReadPointerArray(keySkillArray) : [];
                    var selectedSkillPointers = new HashSet<IntPtr>();
                    if (command.SelectedSkillSlots != null)
                    {
                        foreach (var slot in command.SelectedSkillSlots)
                        {
                            if (slot >= 0 && slot < keySkillPtrs.Length && keySkillPtrs[slot] != IntPtr.Zero)
                            {
                                selectedSkillPointers.Add(keySkillPtrs[slot]);
                            }
                        }
                    }

                    if (skillPointers.Count > 0 && selectedSkillPointers.Count > 0)
                    {
                        int count = Math.Min(skillPointers.Count, arrayLen);
                        for (int i = 0; i < count; i++)
                        {
                            _runtime.WriteBoolArrayElement(isCheckedSkillArray, i, selectedSkillPointers.Contains(skillPointers[i]));
                        }
                    }
                    else
                    {
                        int count = Math.Clamp(arrayLen, 0, 10);
                        var selectedSet = (command.SelectedSkillSlots ?? [0]).ToHashSet();
                        for (int i = 0; i < count; i++)
                        {
                            _runtime.WriteBoolArrayElement(isCheckedSkillArray, i, selectedSet.Contains(i));
                        }
                    }
                }
            }

            if (layout.IsCheckPick >= 0)
            {
                IntPtr isCheckPickArray = _runtime.ReadFieldPtr(autoTrainStatic, layout.IsCheckPick);
                if (isCheckPickArray != IntPtr.Zero)
                {
                    _runtime.WriteBoolArrayElement(isCheckPickArray, 0, command.AutoPick);
                    _runtime.WriteBoolArrayElement(isCheckPickArray, 1, command.PickOthersItems);
                    _runtime.WriteBoolArrayElement(isCheckPickArray, 2, command.PickAllMap);
                    _runtime.WriteBoolArrayElement(isCheckPickArray, 3, command.AutoDropItems);
                }
            }

            if (layout.IsNhat >= 0)
            {
                _runtime.WriteField(autoTrainStatic, layout.IsNhat, true);
            }

            if (layout.StrIdNhat >= 0)
            {
                _runtime.WriteStaticString(GameSchema.AutoTrainMenu.ClassName, layout.StrIdNhat, command.FilterPickIds ?? string.Empty);
            }
            if (layout.StrIdLoaiTru >= 0)
            {
                _runtime.WriteStaticString(GameSchema.AutoTrainMenu.ClassName, layout.StrIdLoaiTru, command.FilterExcludeIds ?? string.Empty);
            }
            if (layout.StrIdVutBo >= 0)
            {
                _runtime.WriteStaticString(GameSchema.AutoTrainMenu.ClassName, layout.StrIdVutBo, command.FilterDropIds ?? string.Empty);
            }

            if (layout.Instance >= 0)
            {
                IntPtr instancePtr = _runtime.ReadFieldPtr(autoTrainStatic, layout.Instance);
                if (instancePtr != IntPtr.Zero)
                {
                    if (layout.TfHpPean >= 0)
                    {
                        IntPtr tfHpPean = _runtime.ReadFieldPtr(instancePtr, layout.TfHpPean);
                        SetTFieldText(tfHpPean, command.PeanHpPercent.ToString());
                    }
                    if (layout.TfMpPean >= 0)
                    {
                        IntPtr tfMpPean = _runtime.ReadFieldPtr(instancePtr, layout.TfMpPean);
                        SetTFieldText(tfMpPean, command.PeanMpPercent.ToString());
                    }
                    if (layout.TfHpOn >= 0)
                    {
                        IntPtr tfHpOn = _runtime.ReadFieldPtr(instancePtr, layout.TfHpOn);
                        SetTFieldText(tfHpOn, command.MinMobHp.ToString());
                    }
                    if (layout.TfHpDuoi >= 0)
                    {
                        IntPtr tfHpDuoi = _runtime.ReadFieldPtr(instancePtr, layout.TfHpDuoi);
                        SetTFieldText(tfHpDuoi, command.MaxMobHp > 10000000000L ? "Không giới hạn" : command.MaxMobHp.ToString());
                    }
                    if (layout.TfIdNhat >= 0)
                    {
                        IntPtr tfIdNhat = _runtime.ReadFieldPtr(instancePtr, layout.TfIdNhat);
                        SetTFieldText(tfIdNhat, command.FilterPickIds ?? string.Empty);
                    }
                    if (layout.TfIdLoaiTru >= 0)
                    {
                        IntPtr tfIdLoaiTru = _runtime.ReadFieldPtr(instancePtr, layout.TfIdLoaiTru);
                        SetTFieldText(tfIdLoaiTru, command.FilterExcludeIds ?? string.Empty);
                    }
                    if (layout.TfIdVutBo >= 0)
                    {
                        IntPtr tfIdVutBo = _runtime.ReadFieldPtr(instancePtr, layout.TfIdVutBo);
                        SetTFieldText(tfIdVutBo, command.FilterDropIds ?? string.Empty);
                    }
                }
            }
        }

        return Task.FromResult(Result.Success(command.Enable ? "Đã bật tự đánh trong game" : "Đã tắt tự đánh trong game"));
    }

    private void SetTFieldText(IntPtr tfieldPtr, string text)
    {
        if (tfieldPtr == IntPtr.Zero) return;
        var tfieldLayout = _runtime.Layout.TField;
        string currentText = _runtime.ReadStringField(tfieldPtr, tfieldLayout.Text);
        if (currentText == text) return;

        _runtime.WriteStringField(tfieldPtr, tfieldLayout.Text, text);
        _runtime.WriteStringField(tfieldPtr, tfieldLayout.PaintedText, text);
        _runtime.WriteField(tfieldPtr, tfieldLayout.CaretPos, text.Length);
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

        long mobHp = _runtime.ReadField<long>(mobPtr, _runtime.Layout.Mob.Hp);
        int mobStatus = _runtime.ReadField<int>(mobPtr, _runtime.Layout.Mob.Status);
        if (mobHp <= 0 || mobStatus == 0 || mobStatus == 1)
            return Result.Failure($"Quái #{command.MobId} đã chết.");

        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Result.Failure("Nhân vật chưa vào game.");

        _runtime.WriteField(charPtr, _runtime.Layout.Char.MobFocus, mobPtr);

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
        PinVectorBackingStorage(vMob, ref _cachedVMobItems);

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
            _runtime.GetClassName(vectorPtr) == GameSchema.MyVector.ClassName)
        {
            IntPtr existingListPtr = _runtime.ReadFieldPtr(vectorPtr, _runtime.Layout.MyVector.A);
            if (existingListPtr != IntPtr.Zero && _runtime.GetClassName(existingListPtr) == "ArrayList")
            {
                if (clearElements)
                {
                    _runtime.CallMethod(GameSchema.MyVector.ClassName, GameSchema.MyVector.MethodRemoveAllElements, vectorPtr);
                }
                return vectorPtr;
            }
        }

        vectorPtr = _runtime.CreateInstance(GameSchema.MyVector.ClassName);
        if (vectorPtr != IntPtr.Zero)
        {
            _runtime.PinObject(vectorPtr);
            IntPtr listPtr = _runtime.ReadFieldPtr(vectorPtr, _runtime.Layout.MyVector.A);
            if (listPtr != IntPtr.Zero)
            {
                _runtime.PinObject(listPtr);
                IntPtr itemsPtr = _runtime.ReadArrayListItemsPtr(listPtr);
                if (itemsPtr != IntPtr.Zero)
                {
                    _runtime.PinObject(itemsPtr);
                }
            }
        }
        return vectorPtr;
    }

    private void PinVectorBackingStorage(IntPtr vectorPtr, ref IntPtr cachedItemsPtr)
    {
        IntPtr listPtr = _runtime.ReadFieldPtr(vectorPtr, _runtime.Layout.MyVector.A);
        if (listPtr == IntPtr.Zero) return;

        IntPtr itemsPtr = _runtime.ReadArrayListItemsPtr(listPtr);
        if (itemsPtr != IntPtr.Zero && itemsPtr != cachedItemsPtr)
        {
            cachedItemsPtr = itemsPtr;
            _runtime.PinObject(itemsPtr);
        }
    }
}
