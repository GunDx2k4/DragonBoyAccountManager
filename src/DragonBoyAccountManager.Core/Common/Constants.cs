namespace DragonBoyAccountManager.Core.Common;

public static class Constants
{
    public const string NullTerminator = "\0";
    public const int MaxPathLength = 260;
    public const int DefaultStringLength = 64;

    public const int PointerSize32 = 4;
    public const int PointerSize64 = 8;

    public const int DefaultPollingIntervalMs = 100;
    public const int DefaultTimeoutMs = 10000;
    public const int ModuleLoadTimeoutMs = 25000;

    public static readonly string[] GameAssemblyModuleNames =
    [
        "GameAssembly.dll"
    ];

    public static class Client
    {
        public const string DefaultSha256Url = "https://data.l7ungdz.id.vn/dragonboy/sha256.json";
        public const string DefaultOffsetsUrl = "https://data.l7ungdz.id.vn/dragonboy/offsets.json";
        public const string DefaultVersion = "2.5.2";
        public const string DefaultSha256 = "CE431629EA6A75E3373B90B37B56918F97B4D3FA1B43875997D13938E3EECCFD";
        public const string LegacyVersion = "2.5.0";
        public const string LegacySha256 = "73BC66D1B6F530FA6DEB95586549B2223A5C957279249F9986A819E31A4E4C3C";
    }
}
