using DragonBoyAccountManager.Core.Game.Entities;
using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Modules;

public interface IPlayerModule
{
    IntPtr Pointer { get; }
    bool IsAlive { get; }
    bool IsInGame { get; }
    bool IsChangingMap { get; }

    int Id { get; }
    string Name { get; }
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
    (bool Success, string Message) UseSkill(int slotIndex);
    (bool Success, string Message) AutoAssignSkills();
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

    public bool IsAlive => Pointer != IntPtr.Zero;
    public bool IsInGame => IsAlive && !string.IsNullOrWhiteSpace(Name);

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

                if (IsAlive)
                {
                    bool isTeleport = _reflector.ReadField<bool>(GameSchema.Char.ClassName, GameSchema.Char.FieldIsTeleport, Pointer);
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

    public int Id => IsAlive ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldCharId, Pointer) : 0;
    public string Name => IsAlive ? _reflector.ReadStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, Pointer) : string.Empty;
    public long Hp => IsAlive ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHp, Pointer) : 0;
    public long MaxHp => IsAlive ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHpFull, Pointer) : 0;
    public long Mp => IsAlive ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMp, Pointer) : 0;
    public long MaxMp => IsAlive ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMpFull, Pointer) : 0;
    public long Power => IsAlive ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldPower, Pointer) : 0;
    public int Gender => IsAlive ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldGender, Pointer) : 0;
    public int X => IsAlive ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, Pointer) : 0;
    public int Y => IsAlive ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, Pointer) : 0;
    public IntPtr MobFocusPtr => IsAlive ? _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMobFocus, Pointer) : IntPtr.Zero;

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
            long hp = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHp, ptr);
            long maxHp = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHpFull, ptr);
            long mp = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMp, ptr);
            long maxMp = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMpFull, ptr);
            long power = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldPower, ptr);
            int gender = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldGender, ptr);
            int x = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, ptr);
            int y = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, ptr);

            bool isInGame = !string.IsNullOrWhiteSpace(name);
            return new PlayerSnapshot(id, name, hp, maxHp, power, gender, x, y, true, isInGame, IsChangingMap, mp, maxMp);
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
        foreach (var item in bag)
        {
            IntPtr bagArrayPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldArrItemBag, Pointer);
            if (bagArrayPtr == IntPtr.Zero) break;
            IntPtr[] itemPtrs = _reflector.ReadMonoPointerArray(bagArrayPtr);
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

    public (bool Success, string Message) UseSkill(int slotIndex)
    {
        if (!IsAlive) return (false, "Nhân vật chưa vào màn hình game!");

        try
        {
            IntPtr skillPtr = IntPtr.Zero;

            IntPtr keySkillArray = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill);
            if (keySkillArray != IntPtr.Zero)
            {
                IntPtr[] skillPtrs = _reflector.ReadMonoPointerArray(keySkillArray);
                if (slotIndex >= 0 && slotIndex < skillPtrs.Length)
                {
                    skillPtr = skillPtrs[slotIndex];
                }
            }

            if (skillPtr == IntPtr.Zero && Pointer != IntPtr.Zero)
            {
                IntPtr vSkillFightPtr = _reflector.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldVSkillFight, Pointer);
                if (vSkillFightPtr != IntPtr.Zero)
                {
                    var vSkills = _reflector.ReadMyVector(vSkillFightPtr);
                    if (slotIndex >= 0 && slotIndex < vSkills.Count)
                    {
                        skillPtr = vSkills[slotIndex];
                    }
                }
            }

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
            int manaUse = _reflector.ReadField<int>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldManaUse, skillPtr);

            IntPtr templatePtr = _reflector.ReadFieldPtr(GameSchema.Skill.ClassName, GameSchema.Skill.FieldTemplate, skillPtr);
            string name = templatePtr != IntPtr.Zero ? _reflector.ReadStringField(GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldName, templatePtr) : "";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = slot == 9 ? "Skill 0" : $"Skill {slot + 1}";
            }

            bool isSelected = (mySkillPtr != IntPtr.Zero && skillPtr == mySkillPtr) || (mySkillId != -1 && skillId == mySkillId);
            return new GameSkillInfo(slot, skillId, name, point, coolDown, manaUse, isSelected);
        }
        catch
        {
            return null;
        }
    }
}
