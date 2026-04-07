namespace MapApi.Models;

public sealed class Poi
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public int RadiusMeters { get; set; }
    public int NearRadiusMeters { get; set; }
    public int DebounceSeconds { get; set; }
    public int CooldownSeconds { get; set; }

    public int? Priority { get; set; }
    public string? MapLink { get; set; }

    public bool IsActive { get; set; }

    // bản “chung” fallback
    public string? NarrationText { get; set; }
    public string? AudioUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string? Language { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}