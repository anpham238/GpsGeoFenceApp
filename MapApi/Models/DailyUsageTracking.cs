namespace MapApi.Models;

public sealed class DailyUsageTracking
{
    public string EntityId { get; set; } = "";       // UserId hoặc DeviceId
    public string ActionType { get; set; } = "";     // QR_SCAN | POI_LISTEN
    public int UsedCount { get; set; }
    public DateTime LastResetAt { get; set; } = DateTime.UtcNow;
}
