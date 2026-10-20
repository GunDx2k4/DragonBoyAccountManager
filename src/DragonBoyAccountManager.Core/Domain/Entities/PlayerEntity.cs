
namespace DragonBoyAccountManager.Core.Domain.Entities;

public sealed class PlayerEntity(MonoReflector reflector, IntPtr pointer) : WorldEntity(reflector, pointer)
{
    public override string ClassName => GameSchema.Char.ClassName;

    public override int Id => GetField<int>(GameSchema.Char.FieldCharId);
    public string Name => GetString(GameSchema.Char.FieldName);
    public long Hp => GetField<long>(GameSchema.Char.FieldHp);
    public long MaxHp => GetField<long>(GameSchema.Char.FieldHpFull);
    public long Power => GetField<long>(GameSchema.Char.FieldPower);
    public int Gender => GetField<int>(GameSchema.Char.FieldGender);
    public override int X => GetField<int>(GameSchema.Char.FieldX);
    public override int Y => GetField<int>(GameSchema.Char.FieldY);
    public sbyte PkType => GetField<sbyte>(GameSchema.Char.FieldCTypePk);
    public bool IsPet => GetField<bool>(GameSchema.Char.FieldIsPet);
    public bool IsMiniPet => GetField<bool>(GameSchema.Char.FieldIsMiniPet);

    public override bool IsAlive => base.IsAlive && Hp > 0;

    public bool IsDisciple => IsPet || IsMiniPet || Name.StartsWith("$") || Name.StartsWith("#") || Name.Contains("Đệ tử");
    public bool IsBoss => !IsDisciple && (PkType == 5 || (Id < 0 && PkType != 0));

    public override string ToString() => $"Player #{Id} ({Name}) [HP: {Hp}/{MaxHp}] at ({X}, {Y})";
}
