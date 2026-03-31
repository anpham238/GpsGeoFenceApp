namespace MapApi.Models
{
    public class Poi
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public float RadiusMeters { get; set; } = 120;
        public float NearRadiusMeters { get; set; } = 220;
        public int DebounceSeconds { get; set; } = 3;
        public int CooldownSeconds { get; set; } = 30;
        public int Priority { get; set; } = 1;
        public string? NarrationText { get; set; }
        public string? AudioUrl { get; set; }
        public string? MapLink { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}