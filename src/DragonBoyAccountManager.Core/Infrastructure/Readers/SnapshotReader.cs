using DragonBoyAccountManager.Core.Domain.Contracts;
using DragonBoyAccountManager.Core.Domain.Snapshots;
using DragonBoyAccountManager.Core.Infrastructure.Mono.Schema;

namespace DragonBoyAccountManager.Core.Infrastructure.Readers;

public static class SnapshotReader
{
    public static PlayerSnapshot ReadPlayerSnapshot(IMonoBridge reflector, IntPtr charPtr, bool isChangingMap)
    {
        if (charPtr == IntPtr.Zero) return PlayerSnapshot.Empty;

        try
        {
            int id = reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldCharId, charPtr);
            string name = reflector.ReadStringField(GameSchema.Char.ClassName, GameSchema.Char.FieldName, charPtr);
            int status = reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldStatusMe, charPtr);
            long hp = reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHp, charPtr);
            long maxHp = reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldHpFull, charPtr);
            long mp = reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMp, charPtr);
            long maxMp = reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldMpFull, charPtr);
            long power = reflector.ReadField<long>(GameSchema.Char.ClassName, GameSchema.Char.FieldPower, charPtr);
            int gender = reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldGender, charPtr);
            int x = reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldX, charPtr);
            int y = reflector.ReadField<int>(GameSchema.Char.ClassName, GameSchema.Char.FieldY, charPtr);

            bool isInGame = !string.IsNullOrWhiteSpace(name);
            bool isAlive = hp > 0 && status != 14 && status != 5;
            return new PlayerSnapshot(id, name, hp, maxHp, power, gender, x, y, isAlive, isInGame, isChangingMap, mp, maxMp);
        }
        catch
        {
            return PlayerSnapshot.Empty;
        }
    }
}
