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

        int charId = _runtime.ReadField<int>(charPtr, _runtime.Layout.Char.CharId);
        _runtime.WriteStatic(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.IsAutoPlay, command.Enable);

        IntPtr autoTrainStatic = _runtime.GetStaticFieldsPtr(GameSchema.AutoTrainMenu.ClassName);
        if (autoTrainStatic != IntPtr.Zero)
        {
            var layout = _runtime.Layout.AutoTrainMenu;

            if (layout.IsChecked >= 0)
            {
                IntPtr isCheckedArray = _runtime.ReadFieldPtr(autoTrainStatic, layout.IsChecked);
                if (isCheckedArray != IntPtr.Zero)
                {
                    int len = _runtime.ReadField<int>(isCheckedArray, 0x18);
                    if (len > 0)
                    {
                        _runtime.Memory.Write<byte>(isCheckedArray + 0x20, (byte)(command.Enable ? 1 : 0));
                    }
                    if (len > 1)
                    {
                        _runtime.Memory.Write<byte>(isCheckedArray + 0x20 + 1, (byte)(command.AvoidSuperMobs ? 1 : 0));
                    }
                    if (len > 2)
                    {
                        _runtime.Memory.Write<byte>(isCheckedArray + 0x20 + 2, (byte)(command.ChangeZoneWhenNoMobs ? 1 : 0));
                    }
                }
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
                    int arrayLen = _runtime.ReadField<int>(isCheckedMobNameArray, 0x18);
                    int count = Math.Min(mobNamePointers.Count, arrayLen);
                    var targetSet = command.TargetMobNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
                    for (int i = 0; i < count; i++)
                    {
                        string name = _runtime.ReadString(mobNamePointers[i]);
                        byte isChecked = (byte)(targetSet.Contains(name) ? 1 : 0);
                        _runtime.Memory.Write<byte>(isCheckedMobNameArray + 0x20 + i, isChecked);
                    }
                }
            }

            if (layout.IsCheckedSkill >= 0)
            {
                IntPtr isCheckedSkillArray = _runtime.ReadFieldPtr(autoTrainStatic, layout.IsCheckedSkill);
                if (isCheckedSkillArray != IntPtr.Zero)
                {
                    int arrayLen = _runtime.ReadField<int>(isCheckedSkillArray, 0x18);
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
                            byte isChecked = (byte)(selectedSkillPointers.Contains(skillPointers[i]) ? 1 : 0);
                            _runtime.Memory.Write<byte>(isCheckedSkillArray + 0x20 + i, isChecked);
                        }
                    }
                    else
                    {
                        int count = Math.Clamp(arrayLen, 0, 10);
                        var selectedSet = (command.SelectedSkillSlots ?? [0]).ToHashSet();
                        for (int i = 0; i < count; i++)
                        {
                            byte isChecked = (byte)(selectedSet.Contains(i) ? 1 : 0);
                            _runtime.Memory.Write<byte>(isCheckedSkillArray + 0x20 + i, isChecked);
                        }
                    }
                }
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
                }
            }

            TryUpdateRmsConfigFile(charId, command);
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

    private static void TryUpdateRmsConfigFile(int charId, SetInGameAutoTrainCommand command)
    {
        if (charId <= 0) return;
        try
        {
            string appDataLow = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Team", "Dragonboy252");
            string filePath = Path.Combine(appDataLow, $"AT_v6_{charId}");
            if (!File.Exists(filePath)) return;

            byte[] bytes = File.ReadAllBytes(filePath);
            using var ms = new MemoryStream(bytes);
            using var reader = new BinaryReader(ms);

            reader.ReadByte();
            reader.ReadByte();
            reader.ReadByte();
            ReadUtf(reader);
            ReadUtf(reader);
            reader.ReadByte();
            ReadUtf(reader);
            ReadUtf(reader);
            int remainingOffset = (int)ms.Position;
            byte[] tail = bytes[remainingOffset..];

            using var outMs = new MemoryStream();
            using var writer = new BinaryWriter(outMs);
            writer.Write((byte)(command.Enable ? 1 : 0));
            writer.Write((byte)(command.AvoidSuperMobs ? 1 : 0));
            writer.Write((byte)(command.ChangeZoneWhenNoMobs ? 1 : 0));
            WriteUtf(writer, command.MinMobHp.ToString());
            WriteUtf(writer, command.MaxMobHp > 10000000000L ? "Không giới hạn" : command.MaxMobHp.ToString());
            writer.Write((byte)(command.AutoUsePean ? 1 : 0));
            WriteUtf(writer, command.PeanHpPercent.ToString());
            WriteUtf(writer, command.PeanMpPercent.ToString());
            writer.Write(tail);

            File.WriteAllBytes(filePath, outMs.ToArray());
        }
        catch
        {
        }
    }

    private static string ReadUtf(BinaryReader reader)
    {
        int len = (reader.ReadByte() << 8) | reader.ReadByte();
        if (len <= 0) return string.Empty;
        byte[] b = reader.ReadBytes(len);
        return System.Text.Encoding.UTF8.GetString(b);
    }

    private static void WriteUtf(BinaryWriter writer, string str)
    {
        byte[] b = System.Text.Encoding.UTF8.GetBytes(str);
        writer.Write((byte)(b.Length >> 8));
        writer.Write((byte)(b.Length & 0xFF));
        if (b.Length > 0)
        {
            writer.Write(b);
        }
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
