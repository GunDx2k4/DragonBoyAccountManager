namespace DragonBoyAccountManager.GUI.Models;

public sealed record CharacterSnapshot(
    string Name,
    long Power,
    long Hp,
    int Gender,
    int MapId = -1,
    int ZoneId = -1,
    string? MapName = null,
    int X = 0,
    int Y = 0,
    int CurrentPlayers = 0,
    int MaxPlayers = 15
);

