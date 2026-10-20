namespace DragonBoyAccountManager.GUI.Models;

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int ServerId { get; set; }
    public string ServerName { get; set; } = "Vũ trụ 1";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLogin { get; set; }
    public float GameSpeed { get; set; } = 1.0f;
    public bool AutoLogin { get; set; } = true;
    public AccountAutoSettings AutoSettings { get; set; } = new();
}
