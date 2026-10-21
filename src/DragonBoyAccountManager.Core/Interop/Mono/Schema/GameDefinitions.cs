namespace DragonBoyAccountManager.Core.Interop.Mono.Schema;

public static class GameDefinitions
{
    public static readonly HashSet<int> PeanTemplateIds =
    [
        13, 60, 61, 62, 63, 64, 65, 352, 523, 595
    ];

    public static readonly HashSet<int> CapsuleTemplateIds =
    [
        193, 194
    ];

    public static readonly HashSet<int> TdltTemplateIds =
    [
        521, 1523, 1524
    ];

    public const sbyte ItemTypePean = 6;

    public static bool IsPean(short templateId, sbyte type) =>
        type == ItemTypePean || PeanTemplateIds.Contains(templateId);

    public static bool IsCapsule(short templateId) =>
        CapsuleTemplateIds.Contains(templateId);

    public static bool IsTdlt(short templateId) =>
        TdltTemplateIds.Contains(templateId);
}
