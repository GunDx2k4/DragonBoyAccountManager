using DragonBoyAccountManager.Core.Common.Results;
using DragonBoyAccountManager.Core.Execution.Commands;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Scrapers;

namespace DragonBoyAccountManager.Core.Execution.Handlers;

public class CombatHandler(IGameRuntime runtime) :
    IGameCommandHandler<AttackMobCommand>,
    IGameCommandHandler<AttackBossCommand>,
    IGameCommandHandler<SelectSkillCommand>,
    IGameCommandHandler<SetInGameAutoTrainCommand>
{
    private struct CachedVectorSlot
    {
        public IntPtr VectorPtr;
        public IntPtr ItemsPtr;
        public uint VectorHandle;
        public uint ListHandle;
        public uint ItemsHandle;
    }

    private readonly IGameRuntime _runtime = runtime;
    private CachedVectorSlot _cachedVMob;
    private CachedVectorSlot _cachedEmptyVChar;
    private CachedVectorSlot _cachedEmptyVMob;
    private CachedVectorSlot _cachedVChar;
    private CachedVectorSlot _cachedSelfVChar;

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

            IntPtr instancePtr = layout.Instance >= 0 ? _runtime.ReadFieldPtr(autoTrainStatic, layout.Instance) : IntPtr.Zero;
            if (instancePtr == IntPtr.Zero)
            {
                instancePtr = _runtime.CallMethod(GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.MethodGI, IntPtr.Zero);
            }

            if (shouldSyncConfig)
            {
                _runtime.CallMethod(GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.MethodScanSkills, IntPtr.Zero);
                _runtime.CallMethod(GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.MethodScanMobsInMap, IntPtr.Zero);
            }

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
                _runtime.CallMethod(GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.MethodSaveAutoTrainConfig, IntPtr.Zero);
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

                    bool hasCheckedSkill = false;
                    if (skillPointers.Count > 0 && selectedSkillPointers.Count > 0)
                    {
                        int count = Math.Min(skillPointers.Count, arrayLen);
                        for (int i = 0; i < count; i++)
                        {
                            bool isMatch = selectedSkillPointers.Contains(skillPointers[i]);
                            _runtime.WriteBoolArrayElement(isCheckedSkillArray, i, isMatch);
                            if (isMatch) hasCheckedSkill = true;
                        }
                    }

                    if (!hasCheckedSkill && arrayLen > 0 && command.Enable)
                    {
                        _runtime.WriteBoolArrayElement(isCheckedSkillArray, 0, true);
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

            if (layout.IsCheckBua >= 0)
            {
                IntPtr isCheckBuaArray = _runtime.ReadFieldPtr(autoTrainStatic, layout.IsCheckBua);
                if (isCheckBuaArray != IntPtr.Zero)
                {
                    var selectedBuaSet = command.SelectedBuaTemplateIds is { Count: > 0 }
                        ? command.SelectedBuaTemplateIds.ToHashSet()
                        : [];
                    foreach (var def in GameDefinitions.AutoTrainBuas)
                    {
                        _runtime.WriteBoolArrayElement(isCheckBuaArray, def.Index, selectedBuaSet.Contains(def.TemplateId));
                    }
                }
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

            if (shouldSyncConfig)
            {
                _runtime.CallMethod(GameSchema.AutoTrainMenu.ClassName, GameSchema.AutoTrainMenu.MethodSaveAutoTrainConfig, IntPtr.Zero);
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
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Result.Failure("Nhân vật chưa vào game.");

        Result? auxResult = await TryExecuteSelectedAuxiliarySkillAsync(charPtr, command.SkillSlot, cancellationToken);
        if (auxResult != null)
            return auxResult;

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

        _runtime.WriteField(charPtr, _runtime.Layout.Char.MobFocus, mobPtr);

        IntPtr vMob = EnsureValidMyVector(ref _cachedVMob, clearElements: true, pinInitialItems: false);
        IntPtr vChar = EnsureValidMyVector(ref _cachedEmptyVChar, clearElements: false, pinInitialItems: true);

        if (vMob == IntPtr.Zero || vChar == IntPtr.Zero)
            return Result.Failure("Không thể tạo vector tấn công.");

        _runtime.CallMethod(GameSchema.MyVector.ClassName, GameSchema.MyVector.MethodAddElement, vMob, mobPtr);
        PinVectorBackingStorage(ref _cachedVMob);

        Result attackResult = ExecutePlayerAttack(charPtr, command.TargetX, vMob, vChar, 1);
        return attackResult.IsSuccess ? Result.Success($"Đã đánh {command.MobName} #{command.MobId}") : attackResult;
    }

    public async Task<Result> HandleAsync(AttackBossCommand command, CancellationToken cancellationToken)
    {
        IntPtr charPtr = _runtime.GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return Result.Failure("Nhân vật chưa vào game.");

        Result? auxResult = await TryExecuteSelectedAuxiliarySkillAsync(charPtr, command.SkillSlot, cancellationToken);
        if (auxResult != null)
            return auxResult;

        IntPtr targetCharPtr = IntPtr.Zero;
        IntPtr vCharInMapPtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.VCharInMap);
        if (vCharInMapPtr != IntPtr.Zero)
        {
            var pointers = CollectionHelper.ReadMyVector(_runtime, vCharInMapPtr);
            foreach (var ptr in pointers)
            {
                if (ptr != IntPtr.Zero && _runtime.ReadField<int>(ptr, _runtime.Layout.Char.CharId) == command.CharId)
                {
                    targetCharPtr = ptr;
                    break;
                }
            }
        }

        if (targetCharPtr == IntPtr.Zero)
            return Result.Failure($"Không tìm thấy mục tiêu #{command.CharId}.");

        long targetHp = _runtime.ReadField<long>(targetCharPtr, _runtime.Layout.Char.Hp);
        if (targetHp <= 0)
            return Result.Failure($"Mục tiêu #{command.CharId} đã bị tiêu diệt.");

        _runtime.WriteField(charPtr, _runtime.Layout.Char.MobFocus, IntPtr.Zero);
        _runtime.WriteField(charPtr, _runtime.Layout.Char.CharFocus, targetCharPtr);

        IntPtr emptyVMob = EnsureValidMyVector(ref _cachedEmptyVMob, clearElements: false, pinInitialItems: true);
        IntPtr vChar = EnsureValidMyVector(ref _cachedVChar, clearElements: true, pinInitialItems: false);
        if (emptyVMob == IntPtr.Zero || vChar == IntPtr.Zero)
            return Result.Failure("Không thể tạo vector tấn công.");

        _runtime.CallMethod(GameSchema.MyVector.ClassName, GameSchema.MyVector.MethodAddElement, vChar, targetCharPtr);
        PinVectorBackingStorage(ref _cachedVChar);

        Result attackResult = ExecutePlayerAttack(charPtr, command.TargetX, emptyVMob, vChar, 2);
        return attackResult.IsSuccess ? Result.Success($"Đã đánh {command.BossName} #{command.CharId}") : attackResult;
    }

    private Result ExecutePlayerAttack(
        IntPtr charPtr,
        int targetX,
        IntPtr vMob,
        IntPtr vChar,
        int attackType)
    {
        int playerX = _runtime.ReadField<int>(charPtr, _runtime.Layout.Char.X);
        int cdir = targetX >= playerX ? 1 : -1;
        _runtime.WriteField(charPtr, _runtime.Layout.Char.Cdir, cdir);

        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Result.Failure("Không tìm thấy Service.");

        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSendPlayerAttack, service, vMob, vChar, attackType);

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        IntPtr mySkillPtr = _runtime.ReadFieldPtr(charPtr, _runtime.Layout.Char.MySkill);
        if (mySkillPtr != IntPtr.Zero)
        {
            _runtime.WriteField(mySkillPtr, _runtime.Layout.Skill.LastTimeUseThisSkill, nowMs);
        }

        return Result.Success();
    }

    private async Task<Result?> TryExecuteSelectedAuxiliarySkillAsync(
        IntPtr charPtr,
        int skillSlot,
        CancellationToken cancellationToken)
    {
        if (skillSlot >= 0)
        {
            await HandleAsync(new SelectSkillCommand(skillSlot), cancellationToken);
        }

        IntPtr mySkillPtr = _runtime.ReadFieldPtr(charPtr, _runtime.Layout.Char.MySkill);
        if (mySkillPtr == IntPtr.Zero)
            return null;

        IntPtr templatePtr = _runtime.ReadFieldPtr(mySkillPtr, _runtime.Layout.Skill.Template);
        if (templatePtr == IntPtr.Zero)
            return null;

        var templateLayout = _runtime.Layout.SkillTemplate;
        int skillType = templateLayout.Type >= 0 ? _runtime.ReadField<int>(templatePtr, templateLayout.Type) : 1;
        if (skillType is not (2 or 3))
            return null;

        IntPtr service = _runtime.GetServicePtr();
        if (service == IntPtr.Zero)
            return Result.Failure("Không tìm thấy Service.");

        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        if (skillType == 3)
        {
            sbyte templateId = templateLayout.Id >= 0 ? _runtime.ReadField<sbyte>(templatePtr, templateLayout.Id) : (sbyte)-1;
            sbyte status = templateId switch
            {
                6 => 0,
                8 => 1,
                12 => 8,
                13 => 6,
                14 => 7,
                19 => 9,
                21 => 10,
                _ => -1
            };

            if (status < 0)
                return Result.Failure($"Kỹ năng hỗ trợ #{templateId} chưa được hỗ trợ thực thi tự động.");

            _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSkillNotFocus, service, status);
            _runtime.WriteField(mySkillPtr, _runtime.Layout.Skill.LastTimeUseThisSkill, nowMs);
            return Result.Success($"Đã kích hoạt kỹ năng hỗ trợ #{templateId}");
        }

        IntPtr emptyVMob = EnsureValidMyVector(ref _cachedEmptyVMob, clearElements: false, pinInitialItems: true);
        if (emptyVMob == IntPtr.Zero)
            return Result.Failure("Không thể tạo vector hỗ trợ.");

        IntPtr selfVChar = EnsureValidMyVector(ref _cachedSelfVChar, clearElements: true, pinInitialItems: false);
        if (selfVChar == IntPtr.Zero)
            return Result.Failure("Không thể tạo vector nhân vật.");

        _runtime.CallMethod(GameSchema.MyVector.ClassName, GameSchema.MyVector.MethodAddElement, selfVChar, charPtr);
        PinVectorBackingStorage(ref _cachedSelfVChar);
        _runtime.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSendPlayerAttack, service, emptyVMob, selfVChar, 2);
        _runtime.WriteField(mySkillPtr, _runtime.Layout.Skill.LastTimeUseThisSkill, nowMs);
        return Result.Success("Đã sử dụng kỹ năng Trị Thương");
    }

    private IntPtr EnsureValidMyVector(ref CachedVectorSlot slot, bool clearElements, bool pinInitialItems)
    {
        if (slot.VectorPtr != IntPtr.Zero &&
            _runtime.GetClassName(slot.VectorPtr) == GameSchema.MyVector.ClassName)
        {
            IntPtr existingListPtr = _runtime.ReadFieldPtr(slot.VectorPtr, _runtime.Layout.MyVector.A);
            if (existingListPtr != IntPtr.Zero && _runtime.GetClassName(existingListPtr) == "ArrayList")
            {
                if (clearElements)
                {
                    _runtime.CallMethod(GameSchema.MyVector.ClassName, GameSchema.MyVector.MethodRemoveAllElements, slot.VectorPtr);
                }
                return slot.VectorPtr;
            }
        }

        UnpinSlot(ref slot);

        IntPtr newVectorPtr = _runtime.CreateInstance(GameSchema.MyVector.ClassName);
        if (newVectorPtr != IntPtr.Zero)
        {
            slot.VectorPtr = newVectorPtr;
            slot.VectorHandle = _runtime.PinObject(newVectorPtr);
            IntPtr listPtr = _runtime.ReadFieldPtr(newVectorPtr, _runtime.Layout.MyVector.A);
            if (listPtr != IntPtr.Zero)
            {
                slot.ListHandle = _runtime.PinObject(listPtr);
                if (pinInitialItems)
                {
                    IntPtr itemsPtr = _runtime.ReadArrayListItemsPtr(listPtr);
                    if (itemsPtr != IntPtr.Zero)
                    {
                        slot.ItemsPtr = itemsPtr;
                        slot.ItemsHandle = _runtime.PinObject(itemsPtr);
                    }
                }
            }
        }

        return slot.VectorPtr;
    }

    private void PinVectorBackingStorage(ref CachedVectorSlot slot)
    {
        if (slot.VectorPtr == IntPtr.Zero) return;
        IntPtr listPtr = _runtime.ReadFieldPtr(slot.VectorPtr, _runtime.Layout.MyVector.A);
        if (listPtr == IntPtr.Zero) return;

        IntPtr itemsPtr = _runtime.ReadArrayListItemsPtr(listPtr);
        if (itemsPtr != IntPtr.Zero && itemsPtr != slot.ItemsPtr)
        {
            if (slot.ItemsHandle != 0)
            {
                _runtime.UnpinObject(slot.ItemsHandle);
            }
            slot.ItemsPtr = itemsPtr;
            slot.ItemsHandle = _runtime.PinObject(itemsPtr);
        }
    }

    private void UnpinSlot(ref CachedVectorSlot slot)
    {
        if (slot.ItemsHandle != 0)
        {
            _runtime.UnpinObject(slot.ItemsHandle);
        }
        if (slot.ListHandle != 0)
        {
            _runtime.UnpinObject(slot.ListHandle);
        }
        if (slot.VectorHandle != 0)
        {
            _runtime.UnpinObject(slot.VectorHandle);
        }
        slot = default;
    }
}
