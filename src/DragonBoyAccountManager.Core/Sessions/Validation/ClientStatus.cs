namespace DragonBoyAccountManager.Core.Sessions.Validation;

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
