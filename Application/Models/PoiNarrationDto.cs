namespace MauiApp1.Models;

public sealed class PoiNarrationDto
{
    public int PoiId { get; set; }
    public byte EventType { get; set; }      // 0 Enter, 1 Near, 2 Tap
    public string Language { get; set; } = "vi-VN";
    public string? NarrationText { get; set; }
}