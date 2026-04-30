namespace MauiApp1.Services.Narration;

public sealed class PoiCandidate
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public string PriorityType { get; set; } = "NORMAL";
    public double DistanceMeters { get; set; }
    public int CooldownSeconds { get; set; }
    public DateTime? LastPlayedAt { get; set; }
    public bool IsTapped { get; set; }
    public bool AllowInterrupt { get; set; }
    public int? TourSortOrder { get; set; }
}

public sealed class PriorityResolverOptions
{
    public double MidZoneThresholdMeters { get; set; } = 5;
    public double TapBoost { get; set; } = 5;
    public double DistanceNearBonus { get; set; } = 3;
    public double CooldownPenalty { get; set; } = 10;
    public double TourBoost { get; set; } = 2;
    public bool EnableTourBoost { get; set; } = true;
}
