
namespace DragonBoyAccountManager.GUI.Models;

public sealed record GameSessionSnapshot(
    PlayerSnapshot Player,
    WorldSnapshot World
);
