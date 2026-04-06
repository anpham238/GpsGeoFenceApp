namespace MapApi.Models;

public sealed class PoiNarration
{
    public long Id { get; set; }
    public string PoiId { get; set; } = "";
    public byte EventType { get; set; }          
    public string LanguageTag { get; set; } = "";
    public string? NarrationText { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
