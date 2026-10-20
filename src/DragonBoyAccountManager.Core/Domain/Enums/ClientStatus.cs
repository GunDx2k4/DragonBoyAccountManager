namespace DragonBoyAccountManager.Core.Domain.Enums;

public enum ClientStatus
{
    Unknown,
    GameNotFound,
    AssemblyNotFound,
    OfficialVersionMatch,
    OutdatedVersion,
    ModifiedOrMod,
    Error
}
