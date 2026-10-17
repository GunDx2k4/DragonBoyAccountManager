using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Entities;

/// <summary>
/// Đại diện cho một vật phẩm đang rơi trên mặt đất (ItemMap).
/// </summary>
public sealed class ItemMapEntity(MonoReflector reflector, IntPtr pointer, string name = "") : WorldEntity(reflector, pointer)
{
    public override string ClassName => GameSchema.ItemMap.ClassName;

    public override int Id => GetField<short>(GameSchema.ItemMap.FieldItemMapId);
    public short TemplateId => GetField<short>(GameSchema.ItemMap.FieldTemplateId);
    public override int X => GetField<int>(GameSchema.ItemMap.FieldX);
    public override int Y => GetField<int>(GameSchema.ItemMap.FieldY);
    public int PlayerId => GetField<int>(GameSchema.ItemMap.FieldPlayerId);

    public string Name { get; init; } = name;

    public override string ToString() => $"ItemMap #{Id} ({Name}) at ({X}, {Y}) [Owner: {PlayerId}]";
}
