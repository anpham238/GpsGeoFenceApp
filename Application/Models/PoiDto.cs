namespace MauiApp1.Models;

public sealed class PoiDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; } = 120;
    public int CooldownSeconds { get; set; } = 30;
    public string? NarrationText { get; set; }
    public string? Language { get; set; } = "vi-VN";
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string? AudioUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; }
}