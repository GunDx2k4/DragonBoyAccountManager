using System.IO;

namespace DragonBoyAccountManager.Core.Sessions;

public static class GameRmsStorage
{
    private static readonly string StorageDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "Team", "Dragonboy252"
    );

    public static void SetServerSelection(int serverId)
    {
        WriteRmsByte("svselect", (byte)serverId);
    }

    public static void SetScreenLevel(int level)
    {
        WriteRmsByte("levelScreenKN", (byte)level);
    }

    public static void SaveGameConfig(GameConfigOptions config)
    {
        ArgumentNullException.ThrowIfNull(config);
        WriteRmsByte("isPlaySound", (byte)(config.IsPlaySound ? 1 : 0));
        WriteRmsByte("lowGraphic", (byte)(config.LowGraphic ? 1 : 0));
        WriteRmsByte("isPaintAura", (byte)(config.IsPaintAura ? 1 : 0));
        WriteRmsByte("isPaintAura2", (byte)(config.IsPaintAura2 ? 1 : 0));
        WriteRmsByte("serverchat", (byte)(config.IsPaintChatVip ? 0 : 1));

        byte screenLevel = (byte)(config.ZoomLevel == 1 ? 1 : 0);
        WriteRmsByte("levelScreenKN", screenLevel);
        WriteRmsByte("lastZoomlevel", (byte)config.ZoomLevel);
    }

    private static void WriteRmsByte(string fileName, byte value)
    {
        try
        {
            if (!Directory.Exists(StorageDir))
            {
                Directory.CreateDirectory(StorageDir);
            }
            string path = Path.Combine(StorageDir, fileName);
            File.WriteAllBytes(path, [value]);
        }
        catch
        {
        }
    }
}
