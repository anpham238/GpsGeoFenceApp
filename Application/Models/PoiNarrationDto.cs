namespace MauiApp1.Models;

public sealed class PoiNarrationDto
{
    public string PoiId { get; set; } = "";
    public byte EventType { get; set; }
    public string Language { get; set; } = "vi-VN";
    public string? NarrationText { get; set; }
}
