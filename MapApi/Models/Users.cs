namespace MapApi.Models;
public sealed class Users
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string Mail { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string PasswordHash { get; set; } = "";
    public string AvatarUrl { get; set; } = "default-avatar.png";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string PlanType { get; set; } = "FREE";
    public DateTime? ProExpiryDate { get; set; }
}
