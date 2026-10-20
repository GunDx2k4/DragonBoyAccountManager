namespace DragonBoyAccountManager.Core.Game.Routines.Login;

public sealed class AutoLoginOptions
{
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public int ServerId { get; init; }
    public string? ServerName { get; init; }
    public int MaxAttempts { get; init; } = 10;

    public AutoLoginOptions() { }

    public AutoLoginOptions(
        string username,
        string password,
        int serverId = 0,
        string? serverName = null,
        int maxAttempts = 10)
    {
        Username = username;
        Password = password;
        ServerId = serverId;
        ServerName = serverName;
        MaxAttempts = maxAttempts;
    }
}
