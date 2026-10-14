using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Objects;

public record GameSkillInfo(int Slot, short SkillId, string Name, int Point, int CoolDown, int ManaUse);

public class GameSkills(MonoReflector reflector) : GameObject(reflector)
{
    public override string ClassName => "GameScr";

    public override IntPtr Pointer
    {
        get
        {
            IntPtr curScreen = Reflector.ReadStatic<IntPtr>("GameCanvas", "currentScreen");
            if (curScreen == IntPtr.Zero || Reflector.GetClassName(curScreen) != "GameScr")
                return IntPtr.Zero;

            IntPtr ptr = ReadStatic<IntPtr>("instance");
            if (ptr == IntPtr.Zero)
            {
                ptr = Reflector.CallMethod("GameScr", "gI", IntPtr.Zero);
            }
            return ptr;
        }
    }

    public List<GameSkillInfo> GetSkills()
    {
        var list = new List<GameSkillInfo>();
        if (!IsAlive) return list;

        try
        {
            // 1. Try reading GameScr.keySkill
            IntPtr keySkillArray = Reflector.ReadStatic<IntPtr>("GameScr", "keySkill");
            if (keySkillArray != IntPtr.Zero)
            {
                IntPtr[] skillPtrs = Reflector.ReadMonoPointerArray(keySkillArray);
                for (int i = 0; i < skillPtrs.Length && i < 10; i++)
                {
                    IntPtr skillPtr = skillPtrs[i];
                    if (skillPtr == IntPtr.Zero) continue;

                    var info = ReadSkillInfo(skillPtr, i);
                    if (info != null)
                    {
                        list.Add(info);
                    }
                }
            }

            // 2. Fallback to Char.myChar.vSkillFight if keySkill was empty
            if (list.Count == 0)
            {
                IntPtr myChar = Reflector.ReadStatic<IntPtr>("Char", "myChar");
                if (myChar == IntPtr.Zero)
                {
                    myChar = Reflector.CallMethod("Char", "myCharz", IntPtr.Zero);
                }

                if (myChar != IntPtr.Zero)
                {
                    IntPtr vSkillFightPtr = Reflector.ReadFieldPtr("Char", "vSkillFight", myChar);
                    if (vSkillFightPtr != IntPtr.Zero)
                    {
                        var vSkills = Reflector.ReadMyVector(vSkillFightPtr);
                        for (int i = 0; i < vSkills.Count && i < 10; i++)
                        {
                            IntPtr skillPtr = vSkills[i];
                            if (skillPtr == IntPtr.Zero) continue;

                            var info = ReadSkillInfo(skillPtr, i);
                            if (info != null)
                            {
                                list.Add(info);
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return list;
    }

    private GameSkillInfo? ReadSkillInfo(IntPtr skillPtr, int slot)
    {
        try
        {
            short skillId = Reflector.ReadField<short>("Skill", "skillId", skillPtr);
            int point = Reflector.ReadField<int>("Skill", "point", skillPtr);
            int coolDown = Reflector.ReadField<int>("Skill", "coolDown", skillPtr);
            int manaUse = Reflector.ReadField<int>("Skill", "manaUse", skillPtr);

            IntPtr templatePtr = Reflector.ReadFieldPtr("Skill", "template", skillPtr);
            string name = templatePtr != IntPtr.Zero ? Reflector.ReadStringField("SkillTemplate", "name", templatePtr) : "";
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

    public (bool Success, string Message) UseSkill(int slotIndex)
    {
        if (!IsAlive)
        {
            return (false, "Nhân vật chưa vào màn hình game!");
        }

        try
        {
            IntPtr skillPtr = IntPtr.Zero;

            // 1. Try reading GameScr.keySkill
            IntPtr keySkillArray = Reflector.ReadStatic<IntPtr>("GameScr", "keySkill");
            if (keySkillArray != IntPtr.Zero)
            {
                IntPtr[] skillPtrs = Reflector.ReadMonoPointerArray(keySkillArray);
                if (slotIndex >= 0 && slotIndex < skillPtrs.Length)
                {
                    skillPtr = skillPtrs[slotIndex];
                }
            }

            // 2. Fallback to Char.myChar.vSkillFight
            if (skillPtr == IntPtr.Zero)
            {
                IntPtr myChar = Reflector.ReadStatic<IntPtr>("Char", "myChar");
                if (myChar == IntPtr.Zero)
                {
                    myChar = Reflector.CallMethod("Char", "myCharz", IntPtr.Zero);
                }

                if (myChar != IntPtr.Zero)
                {
                    IntPtr vSkillFightPtr = Reflector.ReadFieldPtr("Char", "vSkillFight", myChar);
                    if (vSkillFightPtr != IntPtr.Zero)
                    {
                        var vSkills = Reflector.ReadMyVector(vSkillFightPtr);
                        if (slotIndex >= 0 && slotIndex < vSkills.Count)
                        {
                            skillPtr = vSkills[slotIndex];
                        }
                    }
                }
            }

            if (skillPtr == IntPtr.Zero)
            {
                return (false, $"Không tìm thấy kĩ năng ở ô {slotIndex + 1}!");
            }

            IntPtr gameScr = Pointer;
            if (gameScr == IntPtr.Zero)
            {
                return (false, "Không thể truy cập GameScr!");
            }

            // Call doSelectSkill(Skill skill, bool isShortcut)
            Reflector.CallMethod("GameScr", "doSelectSkill", gameScr, skillPtr, true);

            var info = ReadSkillInfo(skillPtr, slotIndex);
            string skillName = info?.Name ?? $"Skill {slotIndex + 1}";
            return (true, $"Đã dùng kĩ năng: {skillName}");
        }
        catch (Exception ex)
        {
            return (false, $"Lỗi khi sử dụng skill: {ex.Message}");
        }
    }
}
