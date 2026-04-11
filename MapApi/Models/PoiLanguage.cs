namespace MapApi.Models;

public sealed class PoiLanguage
{
    public long IdLang { get; set; }
    public string IdPoi { get; set; } = "";
    public string NamePoi { get; set; } = "";
    public string? NarTTS { get; set; }
    public string LanguageTag { get; set; } = "";
    public string? Description { get; set; }
}