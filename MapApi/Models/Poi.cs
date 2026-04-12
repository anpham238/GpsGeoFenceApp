namespace MapApi.Models;

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
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
