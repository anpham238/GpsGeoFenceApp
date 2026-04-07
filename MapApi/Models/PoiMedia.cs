namespace MapApi.Models;

public sealed class PoiMedia
{
    public long Id { get; set; }                 // bigint identity
    public string PoiId { get; set; } = "";      // nvarchar(64)
    public byte MediaType { get; set; }          // tinyint
    public string? LanguageTag { get; set; }     // nvarchar(10) nullable
    public string Url { get; set; } = "";        // nvarchar(1000)
    public string? MimeType { get; set; }        // nvarchar(50) nullable

    public long? FileSizeBytes { get; set; }
    public int? DurationMs { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; }   // datetime2(3)
}