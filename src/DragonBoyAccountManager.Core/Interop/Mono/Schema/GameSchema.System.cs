namespace DragonBoyAccountManager.Core.Interop.Mono.Schema;

public static partial class GameSchema
{
    public static class Main
    {
        public const string ClassName = "Main";
        public const string StaticIsQuitApp = "isQuitApp";
        public const string StaticStarted = "started";
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

    public static class Controller
    {
        public const string ClassName = "Controller";
        public const string StaticIsStopReadMessage = "isStopReadMessage";
        public const string StaticIsLoadingData = "isLoadingData";
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

    public static class Rms
    {
        public const string ClassName = "Rms";
        public const string MethodSaveRMSString = "saveRMSString";
        public const string MethodSaveRMSInt = "saveRMSInt";
        public const string MethodLoadRMSString = "loadRMSString";
        public const string KeyAcc = "acc";
        public const string KeyPass = "pass";
        public const string KeySvSelect = "svselect";
        public const string KeyLevelScreenKN = "levelScreenKN";
        public const string StaticRMS_acc = "RMS_acc";
        public const string StaticRMS_pass = "RMS_pass";
    }

    public static class Namespaces
    {
        public const string SystemCollections = "System.Collections";
        public const string Mscorlib = "mscorlib";
        public const string UnityEngine = "UnityEngine";
        public const string UnityEngineCoreModule = "UnityEngine.CoreModule";
    }

    public static class MyVector
    {
        public const string ClassName = "MyVector";
        public const string FieldA = "a";
        public const string MethodAddElement = "addElement";
        public const string MethodSize = "size";
        public const string MethodElementAt = "elementAt";
    }

    public static class ArrayList
    {
        public const string ClassName = "ArrayList";
        public const string FieldSize = "_size";
        public const string FieldSizeAlt = "size";
        public const string FieldItems = "_items";
        public const string FieldItemsAlt = "items";
    }
}
