namespace MapApi.Models;

public sealed class PoiMedia
{
    public long Id { get; set; }
    public string PoiId { get; set; } = "";
    public byte MediaType { get; set; }         
    public string? LanguageTag { get; set; }   
    public string Url { get; set; } = "";
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? DurationMs { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}