namespace DragonBoyAccountManager.Core.Interop.Mono.Schema;

public static partial class GameSchema
{
    public static class Char
    {
        public const string ClassName = "Char";
        public const string StaticMyChar = "myChar";

        public const string FieldCharId = "charID";
        public const string FieldName = "cName";
        public const string FieldHp = "cHP";
        public const string FieldHpFull = "cHPFull";
        public const string FieldMp = "cMP";
        public const string FieldMpFull = "cMPFull";
        public const string FieldPower = "cPower";
        public const string FieldGender = "cgender";
        public const string FieldX = "cx";
        public const string FieldY = "cy";
        public const string FieldCvx = "cvx";
        public const string FieldCvy = "cvy";
        public const string FieldCxSend = "cxSend";
        public const string FieldCySend = "cySend";
        public const string FieldStatusMe = "statusMe";
        public const string FieldCdir = "cdir";
        public const string FieldCTypePk = "cTypePk";
        public const string FieldIsPet = "isPet";
        public const string FieldIsMiniPet = "isMiniPet";
        public const string FieldArrItemBag = "arrItemBag";
        public const string FieldVSkillFight = "vSkillFight";
        public const string FieldMySkill = "myskill";

        public const string FieldMobFocus = "mobFocus";
        public const string FieldCharFocus = "charFocus";
        public const string FieldNpcFocus = "npcFocus";
        public const string FieldItemFocus = "itemFocus";
        public const string FieldIsManualFocus = "isManualFocus";
        public const string FieldCurrentMovePoint = "currentMovePoint";
        public const string FieldEndMovePointCommand = "endMovePointCommand";
        public const string FieldIsTeleport = "isTeleport";
        public const string FieldTelePortSkill = "telePortSkill";

        public const string StaticIsChangingMap = "ischangingMap";
        public const string StaticIsLoadingMap = "isLoadingMap";

        public const string MethodMyCharz = "myCharz";
        public const string MethodFocusManualTo = "focusManualTo";
    }

    public static class Skill
    {
        public const string ClassName = "Skill";
        public const string FieldSkillId = "skillId";
        public const string FieldPoint = "point";
        public const string FieldCoolDown = "coolDown";
        public const string FieldLastTimeUseThisSkill = "lastTimeUseThisSkill";
        public const string FieldManaUse = "manaUse";
        public const string FieldTemplate = "template";
    }

    public static class SkillTemplate
    {
        public const string ClassName = "SkillTemplate";
        public const string FieldName = "name";
        public const string FieldId = "id";
        public const string FieldIconId = "iconId";
    }

    public static class Mob
    {
        public const string ClassName = "Mob";
        public const string StaticArrMobTemplate = "arrMobTemplate";

        public const string FieldMobId = "mobId";
        public const string FieldTemplateId = "templateId";
        public const string FieldHp = "hp";
        public const string FieldMaxHp = "maxHp";
        public const string FieldStatus = "status";
        public const string FieldX = "x";
        public const string FieldY = "y";
        public const string FieldXFirst = "xFirst";
        public const string FieldYFirst = "yFirst";
        public const string FieldIsDontMove = "isDontMove";
        public const string FieldIsBoss = "isBoss";
        public const string FieldLevelBoss = "levelBoss";
    }

    public static class MobTemplate
    {
        public const string ClassName = "MobTemplate";
        public const string FieldName = "name";
        public const string FieldType = "type";
    }

    public static class Npc
    {
        public const string ClassName = "Npc";
        public const string StaticArrNpcTemplate = "arrNpcTemplate";

        public const string FieldNpcId = "npcId";
        public const string FieldTemplate = "template";
        public const string FieldTemplateId = "templateId";
        public const string FieldCx = "cx";
        public const string FieldCy = "cy";
    }

    public static class NpcTemplate
    {
        public const string ClassName = "NpcTemplate";
        public const string FieldName = "name";
        public const string FieldNpcTemplateId = "npcTemplateId";
    }

    public static class ItemMap
    {
        public const string ClassName = "ItemMap";
        public const string FieldItemMapId = "itemMapID";
        public const string FieldTemplate = "template";
        public const string FieldX = "x";
        public const string FieldY = "y";
        public const string FieldPlayerId = "playerId";
    }

    public static class Item
    {
        public const string ClassName = "Item";
        public const string FieldTemplate = "template";
        public const string FieldQuantity = "quantity";
    }

    public static class ItemTemplate
    {
        public const string ClassName = "ItemTemplate";
        public const string FieldId = "id";
        public const string FieldType = "type";
        public const string FieldName = "name";
    }
}
