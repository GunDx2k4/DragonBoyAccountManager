using DragonBoyAccountManager.Core.Game.Schema;
using DragonBoyAccountManager.Core.Mono;

namespace DragonBoyAccountManager.Core.Game.Entities;

public sealed class MobEntity(MonoReflector reflector, IntPtr pointer, string name = "") : WorldEntity(reflector, pointer)
{
    public override string ClassName => GameSchema.Mob.ClassName;

    public override int Id => GetField<int>(GameSchema.Mob.FieldMobId);
    public int TemplateId => GetField<int>(GameSchema.Mob.FieldTemplateId);
    public long Hp => GetField<long>(GameSchema.Mob.FieldHp);
    public long MaxHp => GetField<long>(GameSchema.Mob.FieldMaxHp);
    public override int X => GetField<int>(GameSchema.Mob.FieldX);
    public override int Y => GetField<int>(GameSchema.Mob.FieldY);
    public int Status => GetField<int>(GameSchema.Mob.FieldStatus);
    public bool IsBoss => GetField<bool>(GameSchema.Mob.FieldIsBoss) || GetField<sbyte>(GameSchema.Mob.FieldLevelBoss) > 0;

    public override bool IsAlive => base.IsAlive && Status != 0 && Status != 1 && Hp > 0;
    public string Name { get; set; } = name;

    public override string ToString() => $"Mob #{Id} ({Name}) [HP: {Hp}/{MaxHp}] at ({X}, {Y})";
}
