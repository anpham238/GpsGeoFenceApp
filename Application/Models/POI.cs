namespace MauiApp1.Models;

public class Poi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    // Khuyên dùng int để khớp schema server (dbo.Pois RadiusMeters/NearRadiusMeters là INT)
    public int RadiusMeters { get; set; } = 120;
    public int NearRadiusMeters { get; set; } = 220;
    public int DebounceSeconds { get; set; } = 3;
    public int CooldownSeconds { get; set; } = 30;
    public int? Priority { get; set; }
    public string? NarrationText { get; set; }
    public string? MapLink { get; set; }
    public string? Language { get; set; } = "vi-VN";  // ngôn ngữ TTS mặc định
    public bool IsActive { get; set; } = true;
    public string? ImageUrl { get; set; }
    public string? AudioUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
