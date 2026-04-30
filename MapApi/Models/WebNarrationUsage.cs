namespace MapApi.Models;

public sealed class WebNarrationUsage
{
    public long Id { get; set; }
    public int PoiId { get; set; }
    public string DeviceKey { get; set; } = string.Empty;
    public int PlayCount { get; set; } = 0;
    public DateTime? LastPlayedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
