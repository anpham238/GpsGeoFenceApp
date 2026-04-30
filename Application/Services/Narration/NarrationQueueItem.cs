namespace MauiApp1.Services.Narration;

public sealed class NarrationQueueItem
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int PriorityLevel { get; set; }
    public string PriorityType { get; set; } = "NORMAL";
    public double DistanceMeters { get; set; }
    public double FinalPriorityScore { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool AllowInterrupt { get; set; }
    public bool IsTapBoosted { get; set; }
}
