namespace MauiApp1.Services;

public static class LanguageService
{
    const string Key = "user_language";

    // 5 ngôn ngữ hỗ trợ: BCP-47 tag + tên hiển thị + cờ
    public static readonly (string Code, string Name, string Flag)[] Supported =
    [
        ("vi-VN", "Tiếng Việt", "🇻🇳"),
        ("en-US", "English",    "🇺🇸"),
        ("ja-JP", "日本語",      "🇯🇵"),
        ("ko-KR", "한국어",      "🇰🇷"),
        ("de-DE", "Deutsch",    "🇩🇪"),
    ];

    /// Lấy ngôn ngữ người dùng đã chọn (mặc định vi-VN)
    public static string Current =>
        Preferences.Get(Key, "vi-VN");

    /// Lưu lựa chọn
    public static void Set(string languageCode) =>
        Preferences.Set(Key, languageCode);
    /// Lấy tên + cờ theo code
    public static string Display(string code) =>
        Supported.FirstOrDefault(x => x.Code == code) is var found && found != default
            ? $"{found.Flag} {found.Name}"
            : code;
}