namespace DragonBoyAccountManager.Core.Accounts;

public class ServerOption
{
    public int Id { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public sbyte Language { get; set; }
    public sbyte TypeSv { get; set; }
    public sbyte IsNew { get; set; }

    public static List<ServerOption> DefaultServers =>
    [
        new() { Id = 0, ServerName = "Vũ trụ 1", Host = "dragon1.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 1, ServerName = "Vũ trụ 2", Host = "dragon2.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 2, ServerName = "Vũ trụ 3", Host = "dragon3.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 3, ServerName = "Vũ trụ 4", Host = "dragon4.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 4, ServerName = "Vũ trụ 5", Host = "dragon5.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 5, ServerName = "Vũ trụ 6", Host = "dragon6.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 6, ServerName = "Vũ trụ 7", Host = "dragon7.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 7, ServerName = "Vũ trụ 8", Host = "dragon10.teamobi.com", Port = 14446, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 8, ServerName = "Vũ trụ 9", Host = "dragon10.teamobi.com", Port = 14447, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 9, ServerName = "Vũ trụ 10", Host = "dragon10.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 10, ServerName = "Vũ trụ 11", Host = "dragon11.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 11, ServerName = "Vũ trụ 12", Host = "dragon12.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 17, ServerName = "Vũ trụ 13", Host = "dragon13.teamobi.com", Port = 14446, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 19, ServerName = "Vũ trụ 14", Host = "dragon14.teamobi.com", Port = 18001, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 20, ServerName = "Vũ trụ 15", Host = "dragon15.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 1 },
        new() { Id = 18, ServerName = "VIP 2", Host = "dragon11.teamobi.com", Port = 18001, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 12, ServerName = "Võ đài liên vũ trụ", Host = "dragonwar.teamobi.com", Port = 14445, Language = 0, TypeSv = 0, IsNew = 0 },
        new() { Id = 13, ServerName = "Universe 1", Host = "dragon.indonaga.com", Port = 14445, Language = 1, TypeSv = 0, IsNew = 0 },
        new() { Id = 16, ServerName = "Indo", Host = "dragon.indonaga.com", Port = 14445, Language = 2, TypeSv = 0, IsNew = 0 },
    ];
}
