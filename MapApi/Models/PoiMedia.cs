namespace MapApi.Models;

public sealed class PoiMedia
{
    public long Idm { get; set; }
    public string IdPoi { get; set; } = "";
    public string? Image { get; set; }
    public string? MapLink { get; set; }
}
