using System.IO;

namespace DragonBoyAccountManager.Core.Sessions;

public static class GameRmsStorage
{
    public static void SetServerSelection(int serverId)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string svSelectPath = Path.Combine(appData, "AppData", "LocalLow", "Team", "Dragonboy252", "svselect");
            string? dir = Path.GetDirectoryName(svSelectPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(svSelectPath, [(byte)serverId]);
        }
        catch
        {
        }
    }

    public static void SetScreenLevel(int level)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string levelPath = Path.Combine(appData, "AppData", "LocalLow", "Team", "Dragonboy252", "levelScreenKN");
            string? dir = Path.GetDirectoryName(levelPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllBytes(levelPath, [(byte)level]);
        }
        catch
        {
        }
    }
}
