namespace MapApi.Contracts.Realtime;

public static class AdminDeviceEvents
{
    // Stable, versioned event names for CMS binding.
    public const string PresenceChangedV1 = "admin.devices.presence.changed.v1";
    public const string PresenceSnapshotV1 = "admin.devices.presence.snapshot.v1";
}

public sealed class DevicePresenceChangedEnvelope
{
    public string Type { get; init; } = "presence.changed";
    public int Version { get; init; } = 1;
    public DateTime EmittedAt { get; init; } = DateTime.UtcNow;
    public DevicePresenceDto Data { get; init; } = new();
}

public sealed class DevicePresenceSnapshotEnvelope
{
    public string Type { get; init; } = "presence.snapshot";
    public int Version { get; init; } = 1;
    public DateTime EmittedAt { get; init; } = DateTime.UtcNow;
    public int OnlineCount { get; init; }
    public int TotalConnectionCount { get; init; }
    public List<DevicePresenceDto> Devices { get; init; } = [];
}

public sealed class DevicePresenceDto
{
    public string DeviceId { get; set; } = "";
    public bool IsOnline { get; set; }
    public int OnlineCount { get; set; }
    public int TotalConnectionCount { get; set; }
    public DateTime LastActiveAt { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
}
