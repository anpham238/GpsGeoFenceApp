using Microsoft.Maui.Storage;
using System.Linq;

namespace MauiApp1.Services;

public static class LanguageService
{
    const string Key = "user_language";

    public static readonly (string Code, string Name, string Flag)[] Supported =
    [
        ("vi-VN", "Tiếng Việt", "🇻🇳"),
        ("en-US", "English", "🇺🇸"),
        ("ja-JP", "日本語", "🇯🇵"),
        ("ko-KR", "한국어", "🇰🇷"),
        ("de-DE", "Deutsch", "🇩🇪"),
    ];

    public static string Current =>
        Preferences.Get(Key, "vi-VN");

    public static void Set(string languageCode) =>
        Preferences.Set(Key, languageCode);

    public static string Display(string code) =>
        Supported.FirstOrDefault(x => x.Code == code) is var found && found != default
            ? $"{found.Flag} {found.Name}"
            : code;
}