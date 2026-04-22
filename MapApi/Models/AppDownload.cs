namespace MapApi.Models;

public sealed class AppDownloadSource
{
    public int SourceId { get; set; }
    public string LocationName { get; set; } = "";
    public string? CampaignCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AnalyticsAppDownloadScan
{
    public long Id { get; set; }
    public int SourceId { get; set; }
    public string Platform { get; set; } = "";
    public DateTime ScannedAt { get; set; }
}
