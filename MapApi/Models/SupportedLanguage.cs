namespace MapApi.Models;

public sealed class SupportedLanguage
{
    public string LanguageTag { get; set; } = "";     // e.g. vi-VN
    public string LanguageName { get; set; } = "";    // e.g. Tiếng Việt
    public bool IsPremium { get; set; }               // true = PRO only
    public bool IsActive { get; set; } = true;
}
