namespace MapApi.Models;

public sealed class HistoryPoi
{
    public long Id { get; set; }
    public string IdPoi { get; set; } = "";
    public Guid IdUser { get; set; }
    public string PoiName { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public DateTime LastVisitedAt { get; set; } = DateTime.UtcNow;
    public int? TotalDurationSeconds { get; set; }
}
