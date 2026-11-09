using System.Collections.Concurrent;
using DragonBoyAccountManager.Core.Interop.Abstractions;
using DragonBoyAccountManager.Core.Interop.Schema;
using DragonBoyAccountManager.Core.Telemetry.Snapshots;

namespace DragonBoyAccountManager.Core.Telemetry.Scrapers;

public class PlayerScraper(IGameRuntime runtime, Func<string?>? originalNameResolver = null) : ISnapshotScraper<PlayerSnapshot>
{
    private readonly IGameRuntime _runtime = runtime;
    private readonly Func<string?>? _originalNameResolver = originalNameResolver;
    private readonly ConcurrentDictionary<short, (sbyte Type, string Name)> _itemTemplateCache = new();
    private readonly ConcurrentDictionary<short, (string Name, int IconId)> _skillTemplateCache = new();

    public PlayerSnapshot Scrape() => ScrapePlayer();

    public IntPtr GetMyCharPtr() => _runtime.GetMyCharPtr();

    public PlayerSnapshot ScrapePlayer()
    {
        IntPtr charPtr = GetMyCharPtr();
        if (charPtr == IntPtr.Zero)
            return PlayerSnapshot.Empty;

        var charLayout = _runtime.Layout.Char;
        int id = _runtime.ReadField<int>(charPtr, charLayout.CharId);
        string name = _runtime.ReadStringField(charPtr, charLayout.Name);
        if (!string.IsNullOrWhiteSpace(name) && name.Contains('*'))
        {
            string? originalName = _originalNameResolver?.Invoke();
            if (!string.IsNullOrWhiteSpace(originalName))
            {
                name = originalName;
            }
        }
        long hp = _runtime.ReadField<long>(charPtr, charLayout.Hp);
        long maxHp = _runtime.ReadField<long>(charPtr, charLayout.HpFull);
        long mp = _runtime.ReadField<long>(charPtr, charLayout.Mp);
        long maxMp = _runtime.ReadField<long>(charPtr, charLayout.MpFull);
        long power = _runtime.ReadField<long>(charPtr, charLayout.Power);
        int gender = _runtime.ReadField<int>(charPtr, charLayout.Gender);
        int x = _runtime.ReadField<int>(charPtr, charLayout.X);
        int y = _runtime.ReadField<int>(charPtr, charLayout.Y);
        int status = _runtime.ReadField<int>(charPtr, charLayout.StatusMe);

        bool isInGame = !string.IsNullOrWhiteSpace(name);
        bool isAlive = hp > 0 && status != 14 && status != 5;
        bool isChangingMap = IsChangingMap(charPtr);
        bool isTdltActive = false;
        bool isAutoPlay = false;
        try
        {
            isTdltActive = _runtime.ReadStatic<bool>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.CanAutoPlay);
            IntPtr autoTrainStatic = _runtime.GetStaticFieldsPtr(GameSchema.AutoTrainMenu.ClassName);
            int isCheckedOffset = _runtime.Layout.AutoTrainMenu.IsChecked;
            if (autoTrainStatic != IntPtr.Zero && isCheckedOffset >= 0)
            {
                IntPtr isCheckedArray = _runtime.ReadFieldPtr(autoTrainStatic, isCheckedOffset);
                if (_runtime.ReadArrayLength(isCheckedArray) > 0)
                {
                    isAutoPlay = _runtime.ReadBoolArrayElement(isCheckedArray, 0);
                }
                else
                {
                    isAutoPlay = _runtime.ReadStatic<bool>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.IsAutoPlay);
                }
            }
            else
            {
                isAutoPlay = _runtime.ReadStatic<bool>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.IsAutoPlay);
            }
        }
        catch { }

        var skills = isInGame ? ScrapeSkills(charPtr) : [];
        var bagItems = isInGame ? ScrapeBagItems(charPtr) : [];
        var activeBuffIcons = isInGame ? ScrapeActiveBuffIconIds() : [];

        return new PlayerSnapshot(
            id, name, hp, maxHp, power, gender, x, y,
            isAlive, isInGame, isChangingMap, mp, maxMp, status, isTdltActive, isAutoPlay, skills, bagItems, activeBuffIcons);
    }

    public bool IsChangingMap(IntPtr charPtr = default)
    {
        if (_runtime.ReadStatic<bool>(GameSchema.Char.ClassName, _runtime.Layout.Char.IsChangingMap)) return true;
        if (_runtime.ReadStatic<bool>(GameSchema.Char.ClassName, _runtime.Layout.Char.IsLoadingMap)) return true;
        if (_runtime.ReadStatic<bool>(GameSchema.GameCanvas.ClassName, _runtime.Layout.GameCanvas.IsLoading)) return true;
        if (_runtime.ReadStatic<bool>(GameSchema.Controller.ClassName, _runtime.Layout.Controller.IsStopReadMessage)) return true;

        if (charPtr != IntPtr.Zero)
        {
            if (_runtime.ReadField<bool>(charPtr, _runtime.Layout.Char.IsTeleport)) return true;
        }
        return false;
    }

    public IReadOnlyList<InventoryItem> ScrapeBagItems(IntPtr charPtr = default)
    {
        var result = new List<InventoryItem>();
        if (charPtr == IntPtr.Zero)
        {
            charPtr = GetMyCharPtr();
        }
        if (charPtr == IntPtr.Zero) return result;

        IntPtr arrBagPtr = _runtime.ReadFieldPtr(charPtr, _runtime.Layout.Char.ArrItemBag);
        if (arrBagPtr == IntPtr.Zero) return result;

        var itemLayout = _runtime.Layout.Item;
        var itemTemplateLayout = _runtime.Layout.ItemTemplate;

        IntPtr[] itemPtrs = _runtime.ReadPointerArray(arrBagPtr);
        for (int i = 0; i < itemPtrs.Length; i++)
        {
            IntPtr itemPtr = itemPtrs[i];
            if (itemPtr == IntPtr.Zero) continue;

            int quantity = _runtime.ReadField<int>(itemPtr, itemLayout.Quantity);
            IntPtr templatePtr = _runtime.ReadFieldPtr(itemPtr, itemLayout.Template);
            if (templatePtr == IntPtr.Zero) continue;

            short templateId = _runtime.ReadField<short>(templatePtr, itemTemplateLayout.Id);
            sbyte type;
            string name;
            if (_itemTemplateCache.TryGetValue(templateId, out var cachedTemplate))
            {
                type = cachedTemplate.Type;
                name = cachedTemplate.Name;
            }
            else
            {
                type = _runtime.ReadField<sbyte>(templatePtr, itemTemplateLayout.Type);
                name = _runtime.ReadStringField(templatePtr, itemTemplateLayout.Name);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    _itemTemplateCache[templateId] = (type, name);
                }
            }

            bool isPean = GameDefinitions.IsPean(templateId, type);
            bool isCapsule = GameDefinitions.IsCapsule(templateId);
            bool isTdlt = GameDefinitions.IsTdlt(templateId);
            int indexUi = itemLayout.IndexUi >= 0 ? _runtime.ReadField<int>(itemPtr, itemLayout.IndexUi) : i;
            sbyte bagIndex = indexUi is >= 0 and <= sbyte.MaxValue ? (sbyte)indexUi : (sbyte)i;

            result.Add(new InventoryItem(
                bagIndex, templateId, name, string.Empty, quantity, type, isPean, isCapsule, isTdlt));
        }

        return result;
    }

    public IReadOnlyList<int> ScrapeActiveBuffIconIds()
    {
        var result = new List<int>();
        int idIconOffset = _runtime.Layout.ItemTime.IdIcon;
        if (idIconOffset < 0) return result;

        try
        {
            if (_runtime.Layout.Char.VItemTime >= 0)
            {
                IntPtr vItemTimePtr = _runtime.ReadStatic<IntPtr>(GameSchema.Char.ClassName, _runtime.Layout.Char.VItemTime);
                if (vItemTimePtr != IntPtr.Zero)
                {
                    foreach (IntPtr ptr in CollectionHelper.ReadMyVector(_runtime, vItemTimePtr))
                    {
                        if (ptr == IntPtr.Zero) continue;
                        short idIcon = _runtime.ReadField<short>(ptr, idIconOffset);
                        if (idIcon > 0 && !result.Contains(idIcon))
                        {
                            result.Add(idIcon);
                        }
                    }
                }
            }

            if (_runtime.Layout.GameScr.TextTime >= 0)
            {
                IntPtr textTimePtr = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.TextTime);
                if (textTimePtr != IntPtr.Zero)
                {
                    foreach (IntPtr ptr in CollectionHelper.ReadMyVector(_runtime, textTimePtr))
                    {
                        if (ptr == IntPtr.Zero) continue;
                        short idIcon = _runtime.ReadField<short>(ptr, idIconOffset);
                        if (idIcon > 0 && !result.Contains(idIcon))
                        {
                            result.Add(idIcon);
                        }
                    }
                }
            }
        }
        catch { }

        return result;
    }

    public IReadOnlyList<GameSkillInfo> ScrapeSkills(IntPtr charPtr = default)
    {
        var result = new List<GameSkillInfo>();
        IntPtr keySkillArray = _runtime.ReadStatic<IntPtr>(GameSchema.GameScr.ClassName, _runtime.Layout.GameScr.KeySkill);
        if (keySkillArray == IntPtr.Zero) return result;

        if (charPtr == IntPtr.Zero)
        {
            charPtr = GetMyCharPtr();
        }
        IntPtr mySkillPtr = charPtr != IntPtr.Zero ? _runtime.ReadFieldPtr(charPtr, _runtime.Layout.Char.MySkill) : IntPtr.Zero;

        var skillLayout = _runtime.Layout.Skill;
        var skillTemplateLayout = _runtime.Layout.SkillTemplate;

        IntPtr[] skillPtrs = _runtime.ReadPointerArray(keySkillArray);
        for (int i = 0; i < skillPtrs.Length && i < 10; i++)
        {
            IntPtr skillPtr = skillPtrs[i];
            if (skillPtr == IntPtr.Zero) continue;

            short skillId = _runtime.ReadField<short>(skillPtr, skillLayout.SkillId);
            int point = _runtime.ReadField<int>(skillPtr, skillLayout.Point);
            int coolDown = (int)_runtime.ReadField<long>(skillPtr, skillLayout.CoolDown);
            long lastTime = _runtime.ReadField<long>(skillPtr, skillLayout.LastTimeUseThisSkill);
            int mana = _runtime.ReadField<int>(skillPtr, skillLayout.ManaUse);

            string name = $"Skill {skillId}";
            int iconId = -1;
            if (_skillTemplateCache.TryGetValue(skillId, out var cachedSkill))
            {
                name = cachedSkill.Name;
                iconId = cachedSkill.IconId;
            }
            else
            {
                IntPtr templatePtr = _runtime.ReadFieldPtr(skillPtr, skillLayout.Template);
                if (templatePtr != IntPtr.Zero)
                {
                    string tmplName = _runtime.ReadStringField(templatePtr, skillTemplateLayout.Name);
                    iconId = _runtime.ReadField<short>(templatePtr, skillTemplateLayout.IconId);
                    if (!string.IsNullOrWhiteSpace(tmplName))
                    {
                        name = tmplName;
                        _skillTemplateCache[skillId] = (tmplName, iconId);
                    }
                }
            }

            bool isSelected = mySkillPtr != IntPtr.Zero && mySkillPtr == skillPtr;
            result.Add(new GameSkillInfo(i, skillId, name, point, coolDown, mana, iconId, isSelected, lastTime));
        }

        return result;
    }
}
