
namespace DragonBoyAccountManager.Core.Infrastructure.Modules;

public interface IPlayerModule
{
    IntPtr Pointer { get; }
    bool IsAlive { get; }
    bool IsInGame { get; }
    bool IsChangingMap { get; }

    int Id { get; }
    string Name { get; }
    int StatusMe { get; }
    long Hp { get; }
    long MaxHp { get; }
    long Mp { get; }
    long MaxMp { get; }
    long Power { get; }
    int Gender { get; }
    int X { get; }
    int Y { get; }
    IntPtr MobFocusPtr { get; }

    PlayerSnapshot GetSnapshot();

    IReadOnlyList<InventoryItem> GetBagItems();
    InventoryItem? FindItem(params short[] templateIds);
    (bool Success, string Message) UseItem(sbyte index, short templateId = -1);
    (bool Success, string Message) UseCapsule();
    (bool Success, string Message) UseTdlt();
    (bool Success, string Message) UsePean();
    bool IsTdltActive { get; }

    IReadOnlyList<GameSkillInfo> GetSkills();
    (bool Success, string Message) SelectSkill(int slotIndex);
    (bool Success, string Message) UseSkill(int slotIndex);
    (bool Success, string Message) AutoAssignSkills();

    IntPtr CurrentMovePoint { get; }
    void SetDirection(int direction);
    void SetSkillLastTimeUsed(long timestampMs);
    void SetVelocityY(int cvy, int statusMe = 3);
    bool SetMoveTarget(int targetX, int targetY);
    void ClearMoveTarget();
    void SetPosition(int x, int y);
}

public class PlayerModule(MonoReflector reflector) : IPlayerModule
{
    private readonly MonoReflector _reflector = reflector;

    public IntPtr Pointer
    {
        get
        {
            try
            {
                return _reflector.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
            }
            catch
            {
                return IntPtr.Zero;
            }
        }
    }

    public bool IsInGame => Pointer != IntPtr.Zero && !string.IsNullOrWhiteSpace(Name);
    public bool IsAlive => Pointer != IntPtr.Zero && Hp > 0 && StatusMe != 14 && StatusMe != 5;

    public bool IsChangingMap
    {
        get
        {
            try
            {
                bool isChanging = _reflector.ReadStatic<bool>(GameSchema.Char.ClassName, GameSchema.Char.StaticIsChangingMap);
                if (isChanging) return true;

                bool isLoading = _reflector.ReadStatic<bool>(GameSchema.Char.ClassName, GameSchema.Char.StaticIsLoadingMap);
                if (isLoading) return true;

                bool canvasLoading = _reflector.ReadStatic<bool>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticIsLoading);
                if (canvasLoading) return true;

                bool isStopMsg = _reflector.ReadStatic<bool>(GameSchema.Controller.ClassName, GameSchema.Controller.StaticIsStopReadMessage);
                if (isStopMsg) return true;

                var ptr = Pointer;
                if (ptr != IntPtr.Zero)
                {
                    bool isTeleport = _reflector.ReadField<bool>(GameSchema.Char.ClassName, GameSchema.Char.FieldIsTeleport, ptr);
                    if (isTeleport) return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }

    public int Id => Pointer != IntPtr.Zero ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldCharId, Pointer) : 0;
    public string Name => Pointer != IntPtr.Zero ? _reflector.ReadStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, Pointer) : string.Empty;
    public int StatusMe => Pointer != IntPtr.Zero ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, Pointer) : 0;
    public long Hp => Pointer != IntPtr.Zero ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHp, Pointer) : 0;
    public long MaxHp => Pointer != IntPtr.Zero ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHpFull, Pointer) : 0;
    public long Mp => Pointer != IntPtr.Zero ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMp, Pointer) : 0;
    public long MaxMp => Pointer != IntPtr.Zero ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMpFull, Pointer) : 0;
    public long Power => Pointer != IntPtr.Zero ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldPower, Pointer) : 0;
    public int Gender => Pointer != IntPtr.Zero ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldGender, Pointer) : 0;
    public int X => Pointer != IntPtr.Zero ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, Pointer) : 0;
    public int Y => Pointer != IntPtr.Zero ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, Pointer) : 0;
    public IntPtr MobFocusPtr => Pointer != IntPtr.Zero ? _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMobFocus, Pointer) : IntPtr.Zero;

    public bool IsTdltActive
    {
        get
        {
            try
            {
                return _reflector.ReadStatic<bool>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticCanAutoPlay);
            }
            catch { return false; }
        }
    }

    public PlayerSnapshot GetSnapshot()
    {
        var ptr = Pointer;
        if (ptr == IntPtr.Zero) return PlayerSnapshot.Empty;

        try
        {
            int id = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldCharId, ptr);
            string name = _reflector.ReadStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, ptr);
            int status = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, ptr);
            long hp = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHp, ptr);
            long maxHp = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHpFull, ptr);
            long mp = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMp, ptr);
            long maxMp = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMpFull, ptr);
            long power = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldPower, ptr);
            int gender = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldGender, ptr);
            int x = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, ptr);
            int y = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, ptr);

            bool isInGame = !string.IsNullOrWhiteSpace(name);
            bool isAlive = hp > 0 && status != 14 && status != 5;
            return new PlayerSnapshot(id, name, hp, maxHp, power, gender, x, y, isAlive, isInGame, IsChangingMap, mp, maxMp);
        }
        catch
        {
            return PlayerSnapshot.Empty;
        }
    }

    public IReadOnlyList<InventoryItem> GetBagItems()
    {
        var result = new List<InventoryItem>();
        if (!IsAlive) return result;

        try
        {
            IntPtr bagArrayPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldArrItemBag, Pointer);
            if (bagArrayPtr == IntPtr.Zero) return result;

            IntPtr[] itemPointers = _reflector.ReadMonoPointerArray(bagArrayPtr);
            for (sbyte i = 0; i < itemPointers.Length; i++)
            {
                IntPtr itemPtr = itemPointers[i];
                if (itemPtr == IntPtr.Zero) continue;

                IntPtr templatePtr = _reflector.ReadFieldPtr(GameSchema.Item.ClassName, GameSchema.Item.FieldTemplate, itemPtr);
                if (templatePtr == IntPtr.Zero) continue;

                short templateId = _reflector.ReadField<short>(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldId, templatePtr);
                string name = _reflector.ReadStringField(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldName, templatePtr);
                int quantity = _reflector.ReadField<int>(GameSchema.Item.ClassName, GameSchema.Item.FieldQuantity, itemPtr);

                result.Add(new InventoryItem(i, templateId, name, quantity));
            }
        }
        catch { }

        return result;
    }

    public InventoryItem? FindItem(params short[] templateIds)
    {
        var bag = GetBagItems();
        foreach (short id in templateIds)
        {
            var match = bag.FirstOrDefault(item => item.TemplateId == id);
            if (match != null) return match;
        }
        return null;
    }

    public (bool Success, string Message) UseItem(sbyte index, short templateId = -1)
    {
        try
        {
            IntPtr service = _reflector.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
            if (service == IntPtr.Zero)
                service = _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);

            if (service == IntPtr.Zero) return (false, "Không thể truy cập Service!");

            _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodUseItem, service, (sbyte)0, (sbyte)1, index, templateId);
            return (true, "Đã gửi lệnh dùng item.");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi dùng vật phẩm: {ex.Message}");
        }
    }

    public (bool Success, string Message) UseCapsule()
    {
        if (!IsAlive) return (false, "Nhân vật chưa vào game!");

        var item = FindItem(194, 193);
        if (item == null) return (false, "Không tìm thấy Capsule (194/193) trong hành trang!");

        return UseItem(-1, item.TemplateId);
    }

    public (bool Success, string Message) UseTdlt()
    {
        if (!IsAlive) return (false, "Nhân vật chưa vào game!");

        var item = FindItem(521, 1523, 1524);
        if (item == null) return (false, "Không tìm thấy Tự Động Luyện Tập (521) trong hành trang!");

        return UseItem(-1, item.TemplateId);
    }

    public (bool Success, string Message) UsePean()
    {
        if (!IsAlive) return (false, "Nhân vật chưa vào game!");

        var pean = FindItem(13, 60, 61, 62, 63, 64, 65, 352, 523, 595);
        if (pean != null)
        {
            return UseItem(-1, pean.TemplateId);
        }

        var bag = GetBagItems();
        IntPtr bagArrayPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldArrItemBag, Pointer);
        if (bagArrayPtr != IntPtr.Zero)
        {
            IntPtr[] itemPtrs = _reflector.ReadMonoPointerArray(bagArrayPtr);
            foreach (var item in bag)
            {
                if (item.Index >= 0 && item.Index < itemPtrs.Length)
                {
                    IntPtr itemPtr = itemPtrs[item.Index];
                    if (itemPtr != IntPtr.Zero)
                    {
                        IntPtr templatePtr = _reflector.ReadFieldPtr(GameSchema.Item.ClassName, GameSchema.Item.FieldTemplate, itemPtr);
                        if (templatePtr != IntPtr.Zero)
                        {
                            sbyte type = _reflector.ReadField<sbyte>(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldType, templatePtr);
                            if (type == 6)
                            {
                                return UseItem(-1, item.TemplateId);
                            }
                        }
                    }
                }
            }
        }

        return (false, "Hết đậu thần trong hành trang!");
    }

    public IReadOnlyList<GameSkillInfo> GetSkills()
    {
        var list = new List<GameSkillInfo>();
        if (!IsAlive) return list;

        try
        {
            IntPtr mySkillPtr = Pointer != IntPtr.Zero ? _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, Pointer) : IntPtr.Zero;
            short mySkillId = -1;
            if (mySkillPtr != IntPtr.Zero)
            {
                mySkillId = _reflector.ReadField<short>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldSkillId, mySkillPtr);
            }

            IntPtr keySkillArray = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill);
            if (keySkillArray != IntPtr.Zero)
            {
                IntPtr[] skillPtrs = _reflector.ReadMonoPointerArray(keySkillArray);
                for (int i = 0; i < skillPtrs.Length && i < 10; i++)
                {
                    IntPtr skillPtr = skillPtrs[i];
                    if (skillPtr == IntPtr.Zero) continue;

                    var info = ReadSkillInfo(skillPtr, i, mySkillPtr, mySkillId);
                    if (info != null) list.Add(info);
                }
            }

            if (list.Count == 0 && Pointer != IntPtr.Zero)
            {
                IntPtr vSkillFightPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldVSkillFight, Pointer);
                if (vSkillFightPtr != IntPtr.Zero)
                {
                    var vSkills = _reflector.ReadMyVector(vSkillFightPtr);
                    for (int i = 0; i < vSkills.Count && i < 10; i++)
                    {
                        IntPtr skillPtr = vSkills[i];
                        if (skillPtr == IntPtr.Zero) continue;

                        var info = ReadSkillInfo(skillPtr, i, mySkillPtr, mySkillId);
                        if (info != null) list.Add(info);
                    }
                }
            }
        }
        catch { }

        return list;
    }

    private IntPtr GetSkillPointerAtSlot(int slotIndex)
    {
        IntPtr keySkillArray = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill);
        if (keySkillArray != IntPtr.Zero)
        {
            IntPtr[] skillPtrs = _reflector.ReadMonoPointerArray(keySkillArray);
            if (slotIndex >= 0 && slotIndex < skillPtrs.Length)
            {
                IntPtr ptr = skillPtrs[slotIndex];
                if (ptr != IntPtr.Zero) return ptr;
            }
        }

        if (Pointer != IntPtr.Zero)
        {
            IntPtr vSkillFightPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldVSkillFight, Pointer);
            if (vSkillFightPtr != IntPtr.Zero)
            {
                var vSkills = _reflector.ReadMyVector(vSkillFightPtr);
                if (slotIndex >= 0 && slotIndex < vSkills.Count)
                {
                    return vSkills[slotIndex];
                }
            }
        }

        return IntPtr.Zero;
    }

    public (bool Success, string Message) SelectSkill(int slotIndex)
    {
        if (!IsAlive) return (false, "Nhân vật chưa vào màn hình game!");

        try
        {
            IntPtr skillPtr = GetSkillPointerAtSlot(slotIndex);
            if (skillPtr == IntPtr.Zero)
            {
                string slotLabel = slotIndex == 9 ? "0" : (slotIndex + 1).ToString();
                return (false, $"Không tìm thấy kỹ năng ở ô phím {slotLabel}!");
            }

            if (Pointer != IntPtr.Zero)
            {
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, Pointer, skillPtr);
            }

            IntPtr gameScr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
            if (gameScr == IntPtr.Zero)
            {
                gameScr = _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);
            }

            if (gameScr != IntPtr.Zero)
            {
                _reflector.WriteField(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldLastSkill, gameScr, skillPtr);
            }

            IntPtr templatePtr = _reflector.ReadFieldPtr(GameSchema.Skill.ClassName, GameSchema.Skill.FieldTemplate, skillPtr);
            if (templatePtr != IntPtr.Zero)
            {
                sbyte templateId = _reflector.ReadField<sbyte>(GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldId, templatePtr);
                IntPtr service = _reflector.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
                if (service == IntPtr.Zero)
                {
                    service = _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
                }
                if (service != IntPtr.Zero)
                {
                    _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSelectSkill, service, (int)templateId);
                }
            }

            var info = ReadSkillInfo(skillPtr, slotIndex);
            string skillName = info?.Name ?? (slotIndex == 9 ? "Skill 0" : $"Skill {slotIndex + 1}");
            return (true, $"Đã chọn kỹ năng: {skillName}");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi khi chọn skill: {ex.Message}");
        }
    }

    public (bool Success, string Message) UseSkill(int slotIndex)
    {
        if (!IsAlive) return (false, "Nhân vật chưa vào màn hình game!");

        try
        {
            IntPtr skillPtr = GetSkillPointerAtSlot(slotIndex);
            if (skillPtr == IntPtr.Zero)
            {
                string slotLabel = slotIndex == 9 ? "0" : (slotIndex + 1).ToString();
                return (false, $"Không tìm thấy kỹ năng ở ô phím {slotLabel}!");
            }

            if (Pointer != IntPtr.Zero)
            {
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, Pointer, skillPtr);
            }

            IntPtr gameScr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
            if (gameScr == IntPtr.Zero)
            {
                gameScr = _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);
            }

            if (gameScr != IntPtr.Zero)
            {
                _reflector.WriteField(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldLastSkill, gameScr, skillPtr);
                _reflector.WriteField(GameSchema.GameScr.ClassName, GameSchema.GameScr.FieldAuto, gameScr, 0);
            }

            _reflector.WriteStatic(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticIsAutoPlay, false);

            IntPtr templatePtr = _reflector.ReadFieldPtr(GameSchema.Skill.ClassName, GameSchema.Skill.FieldTemplate, skillPtr);
            if (templatePtr != IntPtr.Zero)
            {
                sbyte templateId = _reflector.ReadField<sbyte>(GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldId, templatePtr);
                IntPtr service = _reflector.ReadStatic<IntPtr>(GameSchema.Service.ClassName, GameSchema.Service.StaticInstance);
                if (service == IntPtr.Zero)
                {
                    service = _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodGI, IntPtr.Zero);
                }
                if (service != IntPtr.Zero)
                {
                    _reflector.CallMethod(GameSchema.Service.ClassName, GameSchema.Service.MethodSelectSkill, service, (int)templateId);
                }
            }

            if (gameScr != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodDoSelectSkill, gameScr, skillPtr, true);
                _reflector.WriteStatic(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticIsAutoPlay, false);
            }

            var info = ReadSkillInfo(skillPtr, slotIndex);
            string skillName = info?.Name ?? (slotIndex == 9 ? "Skill 0" : $"Skill {slotIndex + 1}");
            return (true, $"Đã dùng kỹ năng: {skillName}");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi khi sử dụng skill: {ex.Message}");
        }
    }

    public (bool Success, string Message) AutoAssignSkills()
    {
        if (!IsAlive) return (false, "Nhân vật chưa vào màn hình game!");

        try
        {
            if (Pointer == IntPtr.Zero) return (false, "Không tìm thấy con trỏ nhân vật!");

            IntPtr vSkillFightPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldVSkillFight, Pointer);
            if (vSkillFightPtr == IntPtr.Zero) return (false, "Không tìm thấy danh sách kỹ năng chiến đấu!");

            var vSkills = _reflector.ReadMyVector(vSkillFightPtr);
            var validSkills = vSkills.Where(s => s != IntPtr.Zero).ToList();
            if (validSkills.Count == 0) return (false, "Nhân vật không có kỹ năng nào để gán!");

            IntPtr keySkillArray = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill);
            IntPtr onScreenSkillArray = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticOnScreenSkill);

            int assignedCount = Math.Min(validSkills.Count, 10);
            for (int i = 0; i < 10; i++)
            {
                IntPtr skillPtr = i < assignedCount ? validSkills[i] : IntPtr.Zero;
                if (keySkillArray != IntPtr.Zero)
                {
                    _reflector.WriteMonoPointerArrayElement(keySkillArray, i, skillPtr);
                }
                if (onScreenSkillArray != IntPtr.Zero)
                {
                    _reflector.WriteMonoPointerArrayElement(onScreenSkillArray, i, skillPtr);
                }
            }

            IntPtr mySkillPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, Pointer);
            if (mySkillPtr == IntPtr.Zero && validSkills.Count > 0)
            {
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, Pointer, validSkills[0]);
            }

            IntPtr gameScr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
            if (gameScr == IntPtr.Zero)
            {
                gameScr = _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);
            }

            if (gameScr != IntPtr.Zero)
            {
                _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodSaveKeySkillToRMS, gameScr);
            }

            return (true, $"Đã tự động gán {assignedCount} kỹ năng vào phím tắt 1 - 0 thành công!");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi khi gán kỹ năng vào phím tắt: {ex.Message}");
        }
    }

    private GameSkillInfo? ReadSkillInfo(IntPtr skillPtr, int slot, IntPtr mySkillPtr = default, short mySkillId = -1)
    {
        try
        {
            short skillId = _reflector.ReadField<short>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldSkillId, skillPtr);
            int point = _reflector.ReadField<int>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldPoint, skillPtr);
            int coolDown = _reflector.ReadField<int>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldCoolDown, skillPtr);
            long lastTimeUse = _reflector.ReadField<long>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldLastTimeUseThisSkill, skillPtr);
            int manaUse = _reflector.ReadField<int>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldManaUse, skillPtr);

            IntPtr templatePtr = _reflector.ReadFieldPtr(GameSchema.Skill.ClassName, GameSchema.Skill.FieldTemplate, skillPtr);
            string name = templatePtr != IntPtr.Zero ? _reflector.ReadStringField(GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldName, templatePtr) : "";
            int iconId = templatePtr != IntPtr.Zero ? _reflector.ReadField<int>(GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldIconId, templatePtr) : -1;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = slot == 9 ? "Skill 0" : $"Skill {slot + 1}";
            }

            bool isSelected = (mySkillPtr != IntPtr.Zero && skillPtr == mySkillPtr) || (mySkillId != -1 && skillId == mySkillId);
            return new GameSkillInfo(slot, skillId, name, point, coolDown, manaUse, iconId, isSelected, lastTimeUse);
        }
        catch
        {
            return null;
        }
    }

    public IntPtr CurrentMovePoint => Pointer != IntPtr.Zero
        ? _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, Pointer)
        : IntPtr.Zero;

    public void SetDirection(int direction)
    {
        var ptr = Pointer;
        if (ptr != IntPtr.Zero)
        {
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCdir, ptr, direction);
        }
    }

    public void SetSkillLastTimeUsed(long timestampMs)
    {
        var ptr = Pointer;
        if (ptr != IntPtr.Zero)
        {
            IntPtr mySkillPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, ptr);
            if (mySkillPtr != IntPtr.Zero)
            {
                _reflector.WriteField(GameSchema.Skill.ClassName, GameSchema.Skill.FieldLastTimeUseThisSkill, mySkillPtr, timestampMs);
            }
        }
    }

    public void SetVelocityY(int cvy, int statusMe = 3)
    {
        var ptr = Pointer;
        if (ptr != IntPtr.Zero)
        {
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, ptr, cvy);
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, ptr, statusMe);
        }
    }

    public bool SetMoveTarget(int targetX, int targetY)
    {
        var ptr = Pointer;
        if (ptr == IntPtr.Zero) return false;

        try
        {
            IntPtr mp = _reflector.CreateInstance(GameSchema.MovePoint.ClassName, targetX, targetY);
            if (mp != IntPtr.Zero)
            {
                int curX = X;
                _reflector.WriteField(GameSchema.MovePoint.ClassName, GameSchema.MovePoint.FieldXEnd, mp, targetX);
                _reflector.WriteField(GameSchema.MovePoint.ClassName, GameSchema.MovePoint.FieldYEnd, mp, targetY);
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, ptr, mp);
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCdir, ptr, targetX >= curX ? 1 : -1);
                _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldEndMovePointCommand, ptr, IntPtr.Zero);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public void ClearMoveTarget()
    {
        var ptr = Pointer;
        if (ptr != IntPtr.Zero)
        {
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCurrentMovePoint, ptr, IntPtr.Zero);
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldEndMovePointCommand, ptr, IntPtr.Zero);
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvx, ptr, 0);
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldCvy, ptr, 0);
        }
    }

    public void SetPosition(int x, int y)
    {
        var ptr = Pointer;
        if (ptr != IntPtr.Zero)
        {
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldX, ptr, x);
            _reflector.WriteField(GameSchema.Char.ClassName, GameSchema.Char.FieldY, ptr, y);
        }
    }
}
