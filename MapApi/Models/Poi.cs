namespace MapApi.Models;

public sealed class Poi
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public int RadiusMeters { get; set; } = 120;
    public int CooldownSeconds { get; set; } = 30;

    public bool IsActive { get; set; } = true;
    public int PriorityLevel { get; set; } = 0;

    // Chính sách xử lý khi 2 POI chồng lấn: "PRIORITY_ONLY" | "FIFO"
    public string ConflictPolicy { get; set; } = "PRIORITY_ONLY";
    // false = chỉ phát POI ưu tiên cao nhất, bỏ qua các POI còn lại trong vùng chồng lấn
    public bool AllowQueueWhenConflict { get; set; } = false;

    // Admin kiểm soát nguồn audio: "AUDIO_FIRST" | "TTS_ONLY" | "AUDIO_ONLY"
    public string AudioSourceMode { get; set; } = "AUDIO_FIRST";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
