namespace MauiApp1.Models; // (Hoặc MapApi.Models tùy bạn đang dùng)

public sealed class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; } = 120;
    public int CooldownSeconds { get; set; } = 30;
    public bool IsActive { get; set; } = true;
    public int PriorityLevel { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // BỔ SUNG CÁC CỘT NÀY ĐỂ NHẬN DỮ LIỆU TỪ API & LƯU VÀO SQLITE
    public string? NarrationText { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string? AudioUrl { get; set; }
    public string? Language { get; set; }
}