namespace MapApi.Models;

public sealed class AnalyticsVisit
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public int PoiId { get; set; }
    public string Action { get; set; } = "";
    public DateTime Timestamp { get; set; }
}

public sealed class AnalyticsRoute
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class AnalyticsListenDuration
{
    public long Id { get; set; }
    public Guid SessionId { get; set; }
    public int PoiId { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime RecordedAt { get; set; }
}
