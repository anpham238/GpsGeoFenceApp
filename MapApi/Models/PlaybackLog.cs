namespace MapApi.Models;

public sealed class PlaybackLog
{
    public long Id { get; set; }                 // bigint identity
    public string PoiId { get; set; } = "";      // nvarchar(64)
    public byte EventType { get; set; }          // tinyint
    public DateTime FiredAtUtc { get; set; }     // datetime2(3)

    public string? DeviceId { get; set; }        // nvarchar(64) null
    public int? DistanceMeters { get; set; }     // int null
}
