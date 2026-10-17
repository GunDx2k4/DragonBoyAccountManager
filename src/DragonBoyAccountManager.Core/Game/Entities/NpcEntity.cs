using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Entities;

/// <summary>
/// Đại diện cho một NPC (Non-Player Character) trên bản đồ.
/// </summary>
public sealed class NpcEntity(MonoReflector reflector, IntPtr pointer, string name = "") : WorldEntity(reflector, pointer)
{
    public override string ClassName => GameSchema.Npc.ClassName;

    public override int Id => GetField<int>(GameSchema.Npc.FieldNpcId);
    public int TemplateId
    {
        get
        {
            try
            {
                IntPtr templatePtr = GetPtr(GameSchema.Npc.FieldTemplate);
                if (templatePtr != IntPtr.Zero)
                    return Reflector.ReadField<int>(GameSchema.NpcTemplate.ClassName, GameSchema.NpcTemplate.FieldNpcTemplateId, templatePtr);
                return GetField<int>(GameSchema.Npc.FieldTemplateId);
            }
            catch
            {
                return Id;
            }
        }
    }
    public override int X => GetField<int>(GameSchema.Npc.FieldCx);
    public override int Y => GetField<int>(GameSchema.Npc.FieldCy);

    public string Name { get; set; } = name;

    public override string ToString() => $"NPC #{Id} ({Name}) at ({X}, {Y})";
}
