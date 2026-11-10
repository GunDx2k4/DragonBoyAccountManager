namespace DragonBoyAccountManager.Core.Interop.Schema;

public sealed record AutoTrainItemDefinition(
    int Index,
    short TemplateId,
    int IconId,
    string Name);

public sealed record AutoTrainBuaDefinition(
    int Index,
    short TemplateId,
    int IconId,
    string Name);

public static class GameDefinitions
{
    public static readonly IReadOnlyList<AutoTrainItemDefinition> AutoTrainItems =
    [
        new(0, 521, 4387, "Tự động luyện tập"),
        new(1, 1628, 13540, "Bùa x2 TN, SM đệ tử"),
        new(2, 381, 2754, "Cuồng nộ"),
        new(3, 1150, 10716, "Cuồng nộ 2"),
        new(4, 384, 2757, "Giáp Xên bọ hung"),
        new(5, 1153, 10712, "Giáp Xên bọ hung 2"),
        new(6, 382, 2755, "Bổ huyết"),
        new(7, 1152, 10714, "Bổ huyết 2"),
        new(8, 383, 2756, "Bổ khí"),
        new(9, 1151, 10715, "Bổ khí 2"),
        new(10, 1517, 12766, "Sát thương chuẩn"),
        new(11, 1518, 12767, "Sát thương chuẩn 2"),
        new(12, 663, 6324, "Bánh Pudding"),
        new(13, 664, 6325, "Xúc xích"),
        new(14, 665, 6326, "Kem dâu"),
        new(15, 666, 6327, "Mì ly"),
        new(16, 667, 6328, "Sushi"),
        new(17, 880, 8060, "Cua rang me"),
        new(18, 881, 8061, "Bạch tuộc nướng"),
        new(19, 882, 8062, "Tôm tẩm bột chiên xù"),
        new(20, 385, 2760, "Ẩn danh"),
        new(21, 1154, 10717, "Ẩn danh 2"),
        new(22, 211, 1394, "Nho tím"),
        new(23, 212, 1395, "Nho xanh"),
        new(24, 2021, 16979, "Phiếu sao vàng may mắn")
    ];

    public static readonly IReadOnlyList<AutoTrainBuaDefinition> AutoTrainBuas =
    [
        new(0, 213, 2754, "Bùa Mạnh Mẽ"),
        new(1, 216, 2756, "Bùa Da Trâu"),
        new(2, 222, 2758, "Bùa Trí Tuệ"),
        new(3, 219, 2755, "Bùa Thu Hút")
    ];

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

    public const short DefaultCapsuleTemplateId = 193;
    public const short DefaultTdltTemplateId = 521;
    public const short DefaultPeanTemplateId = 13;
    public const sbyte ItemTypePean = 6;

    public static bool IsPean(short templateId, sbyte type) =>
        type == ItemTypePean || PeanTemplateIds.Contains(templateId);

    public static bool IsCapsule(short templateId) =>
        CapsuleTemplateIds.Contains(templateId);

    public static bool IsTdlt(short templateId) =>
        TdltTemplateIds.Contains(templateId);
}
