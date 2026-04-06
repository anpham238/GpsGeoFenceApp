namespace MauiApp1.Models;

public sealed class PoiDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public int RadiusMeters { get; set; } = 120;
    public int NearRadiusMeters { get; set; } = 220;

    public int DebounceSeconds { get; set; } = 3;
    public int CooldownSeconds { get; set; } = 30;
    public int? Priority { get; set; }
    public string? NarrationText { get; set; }
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; }
}
