namespace MapApi.Models;

public sealed class PoiLanguage
{
    public long IdLang { get; set; }
    public int IdPoi { get; set; }
    public string LanguageTag { get; set; } = "";
    public string? TextToSpeech { get; set; }
}
