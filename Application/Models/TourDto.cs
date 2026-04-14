namespace MauiApp1.Models;

public sealed class TourDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public List<int> PoiIds { get; set; } = [];
}
