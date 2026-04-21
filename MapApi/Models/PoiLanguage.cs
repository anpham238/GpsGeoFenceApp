namespace MapApi.Models;

public sealed class PoiLanguage
{
    public long IdLang { get; set; }
    public int IdPoi { get; set; }
    public string LanguageTag { get; set; } = "";
    public string? TextToSpeech { get; set; }         // FREE: kịch bản TTS ngắn
    public string? ProPodcastScript { get; set; }     // PRO: kịch bản chuyên sâu
    public string? ProAudioUrl { get; set; }          // PRO: link file âm thanh thu âm
}
