namespace DragonBoyAccountManager.Core.Engine;

public static class GameRmsStorage
{
    public static void SetServerSelection(int serverId)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string svSelectPath = Path.Combine(appData, "AppData", "LocalLow", "Team", "DragonBoy250", "svselect");
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
}
