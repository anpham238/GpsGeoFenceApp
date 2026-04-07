namespace MapApi.Models;

public sealed class PoiNarration
{
    public long Id { get; set; }                 // bigint identity
    public string PoiId { get; set; } = "";      // nvarchar(64)
    public byte EventType { get; set; }          // tinyint
    public string LanguageTag { get; set; } = ""; // nvarchar(10)
    public string? NarrationText { get; set; }   // nvarchar(4000)

    public DateTime CreatedAtUtc { get; set; }   // datetime2(3)
    public DateTime UpdatedAtUtc { get; set; }   // datetime2(3)
}