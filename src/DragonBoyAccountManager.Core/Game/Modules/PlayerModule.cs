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
    long Power { get; }
    int Gender { get; }
    int X { get; }
    int Y { get; }

    PlayerSnapshot GetSnapshot();

    IReadOnlyList<InventoryItem> GetBagItems();
    InventoryItem? FindItem(params short[] templateIds);
    (bool Success, string Message) UseItem(sbyte index, short templateId = -1);
    (bool Success, string Message) UseCapsule();
    (bool Success, string Message) UseTdlt();

    IReadOnlyList<GameSkillInfo> GetSkills();
    (bool Success, string Message) UseSkill(int slotIndex);
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
    public long Power => IsAlive ? _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldPower, Pointer) : 0;
    public int Gender => IsAlive ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldGender, Pointer) : 0;
    public int X => IsAlive ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, Pointer) : 0;
    public int Y => IsAlive ? _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, Pointer) : 0;

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
            long power = _reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldPower, ptr);
            int gender = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldGender, ptr);
            int x = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, ptr);
            int y = _reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, ptr);

            bool isInGame = !string.IsNullOrWhiteSpace(name);
            return new PlayerSnapshot(id, name, hp, maxHp, power, gender, x, y, true, isInGame, IsChangingMap);
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

    public IReadOnlyList<GameSkillInfo> GetSkills()
    {
        var list = new List<GameSkillInfo>();
        if (!IsAlive) return list;

        try
        {
            IntPtr keySkillArray = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill);
            if (keySkillArray != IntPtr.Zero)
            {
                IntPtr[] skillPtrs = _reflector.ReadMonoPointerArray(keySkillArray);
                for (int i = 0; i < skillPtrs.Length && i < 10; i++)
                {
                    IntPtr skillPtr = skillPtrs[i];
                    if (skillPtr == IntPtr.Zero) continue;

                    var info = ReadSkillInfo(skillPtr, i);
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

                        var info = ReadSkillInfo(skillPtr, i);
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
                return (false, $"Không tìm thấy kỹ năng ở ô {slotIndex + 1}!");
            }

            IntPtr gameScr = _reflector.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticInstance);
            if (gameScr == IntPtr.Zero)
            {
                gameScr = _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodGI, IntPtr.Zero);
            }

            if (gameScr == IntPtr.Zero)
            {
                return (false, "Không thể truy cập GameScr!");
            }

            _reflector.CallMethod(GameSchema.GameScr.ClassName, GameSchema.GameScr.MethodDoSelectSkill, gameScr, skillPtr, true);

            var info = ReadSkillInfo(skillPtr, slotIndex);
            string skillName = info?.Name ?? $"Skill {slotIndex + 1}";
            return (true, $"Đã dùng kỹ năng: {skillName}");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi khi sử dụng skill: {ex.Message}");
        }
    }

    private GameSkillInfo? ReadSkillInfo(IntPtr skillPtr, int slot)
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
                name = $"Skill {slot + 1}";
            }

            return new GameSkillInfo(slot, skillId, name, point, coolDown, manaUse);
        }
        catch
        {
            return null;
        }
    }
}
