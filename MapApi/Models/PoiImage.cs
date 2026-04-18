namespace MapApi.Models;

public sealed class PoiImage
{
    public long Id { get; set; }
    public int IdPoi { get; set; }
    public string ImageUrl { get; set; } = "";
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}
