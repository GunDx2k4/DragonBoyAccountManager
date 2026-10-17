using DragonBoyAccountManager.Core.Game.Snapshots;

namespace DragonBoyAccountManager.GUI.Models;

public sealed record GameSessionSnapshot(
    PlayerSnapshot Player,
    WorldSnapshot World
);
