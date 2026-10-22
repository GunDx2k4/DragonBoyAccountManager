namespace DragonBoyAccountManager.Core.Interop.Mono.Schema;

public static partial class GameSchema
{
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
}
