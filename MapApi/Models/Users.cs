namespace MapApi.Models;
public sealed class Users
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string Mail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
