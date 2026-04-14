namespace MauiApp1.Models;

public class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; } // Thêm dấu ? để cho phép Null

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; } = 120;
    public int CooldownSeconds { get; set; } = 30;
    public string? NarrationText { get; set; }
    public string? MapLink { get; set; }
    public string? Language { get; set; } = "vi-VN";
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}