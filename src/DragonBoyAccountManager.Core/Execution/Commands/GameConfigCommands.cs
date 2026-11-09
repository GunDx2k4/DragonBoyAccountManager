using DragonBoyAccountManager.Core.Sessions;

namespace DragonBoyAccountManager.Core.Execution.Commands;

public sealed record SetGameConfigCommand(
    GameConfigOptions Config
) : IGameCommand
{
    public string Name => "Set Game Config";
}
