namespace MapApi.Models;

/// <summary>
/// Entity khop chinh xac voi bang dbo.Pois trong SQL Server.
/// Khong co RowVer, Geo (EF se bao loi khi insert neu them vao).
/// </summary>
public class Poi
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // float trong C# = REAL trong SQL Server (4 byte)
    public float RadiusMeters { get; set; } = 120;
    public float NearRadiusMeters { get; set; } = 220;

    public int DebounceSeconds { get; set; } = 3;
    public int CooldownSeconds { get; set; } = 30;
    public int Priority { get; set; } = 1;

    // Noi dung thuyet minh
    public string? Language { get; set; } = "vi-VN";
    public string? NarrationText { get; set; }
    public string? AudioUrl { get; set; }

    // Ban do & hinh anh
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }

    // Trang thai
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class PlaybackLog
{
    public long Id { get; set; }          // BIGINT IDENTITY
    public string DeviceId { get; set; } = "";
    public string PoiId { get; set; } = "";
    public string TriggerType { get; set; } = "";    // ENTER / NEAR / TAP
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
    public int? DurationListened { get; set; }
    public bool IsSuccess { get; set; } = true;

    // Navigation property (load kem ten POI khi can)
    public Poi? Poi { get; set; }
}