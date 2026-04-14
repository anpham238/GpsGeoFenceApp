namespace MapApi.Models;

public sealed class Tour
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public List<TourPoi> TourPois { get; set; } = [];
}

public sealed class TourPoi
{
    public int TourId { get; set; }
    public int PoiId { get; set; }
    public int SortOrder { get; set; }
}
