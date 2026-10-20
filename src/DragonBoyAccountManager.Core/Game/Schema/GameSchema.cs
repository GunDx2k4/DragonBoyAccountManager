namespace DragonBoyAccountManager.Core.Game.Schema;

public static class GameSchema
{
    public static class GameCanvas
    {
        public const string ClassName = "GameCanvas";
        public const string StaticInstance = "instance";
        public const string StaticBRun = "bRun";
        public const string StaticGameCanvas = "gameCanvas";
        public const string StaticCurrentScreen = "currentScreen";
        public const string StaticCurrentDialog = "currentDialog";
        public const string StaticServerScreen = "serverScreen";
        public const string StaticServerScr = "serverScr";
        public const string StaticLoginScr = "loginScr";
        public const string StaticMenu = "menu";
        public const string StaticPanel = "panel";
        public const string StaticIsLoading = "isLoading";

        public const string MethodConnect = "connect";
        public const string MethodEndDlg = "endDlg";
        public const string MethodStartOKDlg = "startOKDlg";
        public const string MethodStartWaitDlg = "startWaitDlg";
        public const string MethodKeyPressedz = "keyPressedz";
        public const string MethodKeyReleasedz = "keyReleasedz";
    }

    public static class Screen
    {
        public const string ClassName = "mScreen";
        public const string MethodPerform = "perform";
        public const string MethodSwitchToMe = "switchToMe";
    }

    public static class LoginScr
    {
        public const string ClassName = "LoginScr";
        public const string FieldTfUser = "tfUser";
        public const string FieldTfPass = "tfPass";
        public const string StaticServerName = "serverName";
        public const string MethodDoLogin = "doLogin";

        public const int ActionSubmit = 888393;
        public const int ActionCancel = 101;
    }

    public static class ServerListScreen
    {
        public const string ClassName = "ServerListScreen";
        public const string StaticIpSelect = "ipSelect";
        public const string StaticNameServer = "nameServer";
        public const string StaticAddress = "address";
        public const string StaticPort = "port";
        public const string MethodSetIpSelect = "SetIpSelect";
        public const string MethodSelectServer = "selectServer";
        public const string MethodConnectIP = "ConnectIP";

        public const int ActionSelectServer = 6;
        public const int ActionOpenLogin = 7;
        public const int ActionLoginSaved = 3;
        public const int ActionDownload = 2;
        public const string StaticCmdDownload = "cmdDownload";
        public const string StaticLoadScreen = "loadScreen";
        public const string StaticIsGetData = "isGetData";
        public const string StaticBigOk = "bigOk";
        public const string StaticPercent = "percent";
    }

    public static class Command
    {
        public const string ClassName = "Command";
        public const string MethodPerformAction = "performAction";
    }

    public static class SplashScr
    {
        public const string ClassName = "SplashScr";
        public const string StaticNData = "nData";
        public const string StaticMaxData = "maxData";
    }

    public static class TField
    {
        public const string ClassName = "TField";
        public const string FieldText = "text";
        public const string FieldPaintedText = "paintedText";
        public const string FieldPasswordText = "passwordText";
        public const string FieldCaretPos = "caretPos";
        public const string MethodSetText = "setText";
        public const string MethodGetText = "getText";
    }

    public static class Rms
    {
        public const string ClassName = "Rms";
        public const string MethodSaveRMSString = "saveRMSString";
        public const string MethodSaveRMSInt = "saveRMSInt";
        public const string KeyAcc = "acc";
        public const string KeyPass = "pass";
        public const string KeySvSelect = "svselect";
        public const string KeyLevelScreenKN = "levelScreenKN";
    }

    public static class GameMidlet
    {
        public const string ClassName = "GameMidlet";
        public const string StaticVersion = "VERSION";
        public const string StaticIntVersion = "intVERSION";
        public const string StaticIp = "IP";
        public const string StaticPort = "PORT";
    }

    public static class Session
    {
        public const string ClassName = "Session_ME";
        public const string StaticConnected = "connected";
        public const string StaticConnecting = "connecting";
        public const string StaticGetKeyComplete = "getKeyComplete";
        public const string MethodGI = "gI";
        public const string MethodClose = "close";
        public const string MethodConnect = "connect";
    }

    public static class GameScr
    {
        public const string ClassName = "GameScr";
        public const string StaticInstance = "instance";
        public const string StaticVMob = "vMob";
        public const string StaticVNpc = "vNpc";
        public const string StaticVCharInMap = "vCharInMap";
        public const string StaticVItemMap = "vItemMap";
        public const string StaticKeySkill = "keySkill";
        public const string StaticOnScreenSkill = "onScreenSkill";
        public const string StaticIsAutoPlay = "isAutoPlay";
        public const string StaticCanAutoPlay = "canAutoPlay";

        public const string FieldZones = "zones";
        public const string FieldMaxPlayer = "maxPlayer";
        public const string FieldNumPlayer = "numPlayer";
        public const string FieldPts = "pts";
        public const string FieldClickMoving = "clickMoving";
        public const string FieldAuto = "auto";
        public const string FieldLastSkill = "lastSkill";

        public const string MethodGI = "gI";
        public const string MethodCheckClickMoveTo = "checkClickMoveTo";
        public const string MethodDoDoubleClickToObj = "doDoubleClickToObj";
        public const string MethodDoSelectSkill = "doSelectSkill";
        public const string MethodSaveKeySkillToRMS = "saveKeySkillToRMS";
        public const string MethodSaveOnScreenSkillToRMS = "saveonScreenSkillToRMS";
    }

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

    public static class Controller
    {
        public const string ClassName = "Controller";
        public const string StaticIsStopReadMessage = "isStopReadMessage";
        public const string StaticIsLoadingData = "isLoadingData";
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

    public static class TileMap
    {
        public const string ClassName = "TileMap";
        public const string StaticMapId = "mapID";
        public const string StaticZoneId = "zoneID";
        public const string StaticMapName = "mapName";
        public const string StaticMapNames = "mapNames";
        public const string StaticTmw = "tmw";
        public const string StaticTmh = "tmh";
        public const string StaticPxw = "pxw";
        public const string StaticPxh = "pxh";
        public const string StaticTypes = "types";
        public const string StaticMaps = "maps";
        public const string StaticVGo = "vGo";
        public const string MethodTileTypeAt = "tileTypeAt";
    }

    public static class Waypoint
    {
        public const string ClassName = "Waypoint";
        public const string FieldMinX = "minX";
        public const string FieldMinY = "minY";
        public const string FieldMaxX = "maxX";
        public const string FieldMaxY = "maxY";
        public const string FieldIsEnter = "isEnter";
        public const string FieldIsOffline = "isOffline";
        public const string FieldPopup = "popup";

        public const string MethodPerform = "perform";
    }

    public static class PopUp
    {
        public const string ClassName = "PopUp";
        public const string FieldSays = "says";
    }

    public static class MovePoint
    {
        public const string ClassName = "MovePoint";
        public const string FieldXEnd = "xEnd";
        public const string FieldYEnd = "yEnd";
    }

    public static class Service
    {
        public const string ClassName = "Service";
        public const string StaticInstance = "instance";
        public const string MethodGI = "gI";
        public const string MethodCharMove = "charMove";
        public const string MethodRequestChangeZone = "requestChangeZone";
        public const string MethodOpenUIZone = "openUIZone";
        public const string MethodRequestChangeMap = "requestChangeMap";
        public const string MethodGetMapOffline = "getMapOffline";
        public const string MethodUseItem = "useItem";
        public const string MethodChat = "chat";
        public const string MethodChatGlobal = "chatGlobal";
        public const string MethodSetClientType = "setClientType";
        public const string MethodLogin = "login";
        public const string MethodPickItem = "pickItem";
        public const string MethodSendPlayerAttack = "sendPlayerAttack";
        public const string MethodSelectSkill = "selectSkill";
        public const string MethodWakeUpFromDead = "wakeUpFromDead";
    }

    public static class MyVector
    {
        public const string ClassName = "MyVector";
        public const string MethodAddElement = "addElement";
        public const string MethodSize = "size";
        public const string MethodElementAt = "elementAt";
    }

    public static class MsgDlg
    {
        public const string ClassName = "MsgDlg";
        public const string FieldIsWait = "isWait";
        public const string FieldInfo = "info";
    }

    public static class Panel
    {
        public const string ClassName = "Panel";
        public const string FieldIsShow = "isShow";
        public const string FieldType = "type";
        public const string FieldTimeShow = "timeShow";
        public const string FieldIsClose = "isClose";
        public const string MethodHide = "hide";
        public const string MethodHideNow = "hideNow";
        public const int TypeZone = 3;
    }

    public static class Main
    {
        public const string ClassName = "Main";
        public const string StaticIsQuitApp = "isQuitApp";
        public const string StaticStarted = "started";
    }
}
