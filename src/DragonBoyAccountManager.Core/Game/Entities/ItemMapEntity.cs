using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Entities;

public sealed class ItemMapEntity(MonoReflector reflector, IntPtr pointer, string name = "") : WorldEntity(reflector, pointer)
{
    public override string ClassName => GameSchema.ItemMap.ClassName;

    public override int Id => GetField<int>(GameSchema.ItemMap.FieldItemMapId);

    public short TemplateId
    {
        get
        {
            try
            {
                IntPtr tmplPtr = GetPtr(GameSchema.ItemMap.FieldTemplate);
                if (tmplPtr != IntPtr.Zero)
                {
                    return Reflector.ReadField<short>(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldId, tmplPtr);
                }
            }
            catch { }
            return -1;
        }
    }

    public override int X => GetField<int>(GameSchema.ItemMap.FieldX);
    public override int Y => GetField<int>(GameSchema.ItemMap.FieldY);
    public int PlayerId => GetField<int>(GameSchema.ItemMap.FieldPlayerId);

    public string Name
    {
        get
        {
            if (!string.IsNullOrEmpty(name)) return name;
            try
            {
                IntPtr tmplPtr = GetPtr(GameSchema.ItemMap.FieldTemplate);
                if (tmplPtr != IntPtr.Zero)
                {
                    string n = Reflector.ReadStringField(GameSchema.ItemTemplate.ClassName, GameSchema.ItemTemplate.FieldName, tmplPtr);
                    if (!string.IsNullOrEmpty(n)) return n;
                }
            }
            catch { }
            return $"Vật phẩm #{Id}";
        }
    }

    public override string ToString() => $"ItemMap #{Id} ({Name}) at ({X}, {Y}) [Owner: {PlayerId}]";
}
