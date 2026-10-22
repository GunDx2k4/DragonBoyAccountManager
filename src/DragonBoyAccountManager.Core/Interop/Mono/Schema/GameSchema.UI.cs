namespace DragonBoyAccountManager.Core.Interop.Mono.Schema;

public static partial class GameSchema
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

        public const string StaticIsUpdateAll = "isUpdateAll";
        public const string StaticIsUpdateData = "isUpdateData";
        public const string StaticIsUpdateMap = "isUpdateMap";
        public const string StaticIsUpdateSkill = "isUpdateSkill";
        public const string StaticIsUpdateItem = "isUpdateItem";

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
        public const string StaticWaitToLogin = "waitToLogin";
        public const string FieldCmd = "cmd";
    }

    public static class Command
    {
        public const string ClassName = "Command";
        public const string FieldCaption = "caption";
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
        public const string FieldInputType = "inputType";
        public const string FieldCaretPos = "caretPos";
        public const string MethodSetText = "setText";
        public const string MethodGetText = "getText";
        public const int InputTypePassword = 2;
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

    public static class SelectCharScr
    {
        public const string ClassName = "SelectCharScr";
        public const int ActionSelect = 100;
    }

    public static class ChooseCharScr
    {
        public const string ClassName = "ChooseCharScr";
        public const int ActionSelect = 1;
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
}
