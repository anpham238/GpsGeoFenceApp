namespace MapApi.Models;

public sealed class GuestDevice
{
    public string DeviceId { get; set; } = "";
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;
}
