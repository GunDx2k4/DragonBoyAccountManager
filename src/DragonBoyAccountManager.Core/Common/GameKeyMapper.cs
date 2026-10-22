namespace DragonBoyAccountManager.Core.Common;

public static class GameKeyMapper
{
    public static int MapToKeyCode(GameKey key) => key switch
    {
        GameKey.Up => -1,
        GameKey.Down => -2,
        GameKey.Left => -3,
        GameKey.Right => -4,
        GameKey.Ok => -5,
        GameKey.Focus => -26,
        GameKey.PickItem => 113,
        GameKey.UsePean => 32,
        GameKey.ShowPanel => -21,
        GameKey.HidePanel => -22,
        GameKey.Yes => 121,
        GameKey.Chat => 114,
        GameKey.Back => -8,
        _ => 0
    };
}
