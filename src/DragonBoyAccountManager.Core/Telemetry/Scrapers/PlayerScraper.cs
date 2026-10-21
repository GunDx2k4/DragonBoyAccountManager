using DragonBoyAccountManager.Core.Interop.Mono;
using DragonBoyAccountManager.Core.Interop.Mono.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public class PlayerScraper(IMonoRuntime mono)
{
    private readonly IMonoRuntime _mono = mono;

    public IntPtr GetMyCharPtr()
    {
        return _mono.ReadStatic<IntPtr>(GameSchema.Char.ClassName, GameSchema.Char.StaticMyChar);
    }

    public PlayerSnapshot ScrapePlayer()
    {
        IntPtr charPtr = GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return PlayerSnapshot.Empty;

        int id = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldCharId, charPtr);
        string name = _mono.ReadStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, charPtr);
        long hp = _mono.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHp, charPtr);
        long maxHp = _mono.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHpFull, charPtr);
        long mp = _mono.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMp, charPtr);
        long maxMp = _mono.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMpFull, charPtr);
        long power = _mono.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldPower, charPtr);
        int gender = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldGender, charPtr);
        int x = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, charPtr);
        int y = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, charPtr);
        int status = _mono.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, charPtr);

        bool isInGame = !string.IsNullOrWhiteSpace(name);
        bool isAlive = hp > 0 && status != 14 && status != 5;
        bool isChangingMap = IsChangingMap(charPtr);
        bool isTdltActive = false;
        try
        {
            isTdltActive = _mono.ReadStatic<bool>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticCanAutoPlay);
        }
        catch { }

        return new PlayerSnapshot(
            id, name, hp, maxHp, power, gender, x, y,
            isAlive, isInGame, isChangingMap, mp, maxMp, status, isTdltActive);
    }

    public bool IsChangingMap(IntPtr charPtr = default)
    {
        if (_mono.ReadStatic<bool>(GameSchema.Char.ClassName, GameSchema.Char.StaticIsChangingMap)) return true;
        if (_mono.ReadStatic<bool>(GameSchema.Char.ClassName, GameSchema.Char.StaticIsLoadingMap)) return true;
        if (_mono.ReadStatic<bool>(GameSchema.GameCanvas.ClassName, GameSchema.GameCanvas.StaticIsLoading)) return true;
        if (_mono.ReadStatic<bool>(GameSchema.Controller.ClassName, GameSchema.Controller.StaticIsStopReadMessage)) return true;

        if (charPtr != IntPtr.Zero)
        {
            if (_mono.ReadField<bool>(GameSchema.Char.ClassName, GameSchema.Char.FieldIsTeleport, charPtr)) return true;
        }
        return false;
    }

    public IReadOnlyList<InventoryItem> ScrapeBagItems()
    {
        var result = new List<InventoryItem>();
        IntPtr charPtr = GetMyCharPtr();
        if (charPtr == IntPtr.Zero) return result;

        IntPtr arrBagPtr = _mono.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldArrItemBag, charPtr);
        if (arrBagPtr == IntPtr.Zero) return result;

        IntPtr[] itemPtrs = _mono.ReadMonoPointerArray(arrBagPtr);
        for (int i = 0; i < itemPtrs.Length; i++)
        {
            IntPtr itemPtr = itemPtrs[i];
            if (itemPtr == IntPtr.Zero) continue;

            int quantity = _mono.ReadField<int>(GameSchema.Item.ClassName, GameSchema.Item.FieldQuantity, itemPtr);
            IntPtr templatePtr = _mono.ReadFieldPtr(GameSchema.Item.ClassName, GameSchema.Item.FieldTemplate, itemPtr);
            if (templatePtr == IntPtr.Zero) continue;

            short templateId = _mono.ReadField<short>(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldId, templatePtr);
            sbyte type = _mono.ReadField<sbyte>(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldType, templatePtr);
            string name = _mono.ReadStringField(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldName, templatePtr);

            bool isPean = GameDefinitions.IsPean(templateId, type);
            bool isCapsule = GameDefinitions.IsCapsule(templateId);
            bool isTdlt = GameDefinitions.IsTdlt(templateId);

            result.Add(new InventoryItem(
                (sbyte)i, templateId, name, string.Empty, quantity, type, isPean, isCapsule, isTdlt));
        }

        return result;
    }

    public IReadOnlyList<GameSkillInfo> ScrapeSkills()
    {
        var result = new List<GameSkillInfo>();
        IntPtr keySkillArray = _mono.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, GameSchema.GameScr.StaticKeySkill);
        if (keySkillArray == IntPtr.Zero) return result;

        IntPtr charPtr = GetMyCharPtr();
        IntPtr mySkillPtr = charPtr != IntPtr.Zero ? _mono.ReadFieldPtr(GameSchema.Char.ClassName, GameSchema.Char.FieldMySkill, charPtr) : IntPtr.Zero;

        IntPtr[] skillPtrs = _mono.ReadMonoPointerArray(keySkillArray);
        for (int i = 0; i < skillPtrs.Length && i < 10; i++)
        {
            IntPtr skillPtr = skillPtrs[i];
            if (skillPtr == IntPtr.Zero) continue;

            short skillId = _mono.ReadField<short>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldSkillId, skillPtr);
            int point = _mono.ReadField<int>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldPoint, skillPtr);
            int coolDown = (int)_mono.ReadField<long>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldCoolDown, skillPtr);
            long lastTime = _mono.ReadField<long>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldLastTimeUseThisSkill, skillPtr);
            int mana = _mono.ReadField<int>(GameSchema.Skill.ClassName, GameSchema.Skill.FieldManaUse, skillPtr);

            string name = $"Skill {skillId}";
            int iconId = -1;
            IntPtr templatePtr = _mono.ReadFieldPtr(GameSchema.Skill.ClassName, GameSchema.Skill.FieldTemplate, skillPtr);
            if (templatePtr != IntPtr.Zero)
            {
                string tmplName = _mono.ReadStringField(GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldName, templatePtr);
                if (!string.IsNullOrWhiteSpace(tmplName))
                    name = tmplName;
                iconId = _mono.ReadField<short>(GameSchema.SkillTemplate.ClassName, GameSchema.SkillTemplate.FieldIconId, templatePtr);
            }

            bool isSelected = mySkillPtr != IntPtr.Zero && mySkillPtr == skillPtr;
            result.Add(new GameSkillInfo(i, skillId, name, point, coolDown, mana, iconId, isSelected, lastTime));
        }

        return result;
    }
}
