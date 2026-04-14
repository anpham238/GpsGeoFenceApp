using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MapApi.Services;
public sealed class TranslatorClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    // Map BCP-47 tag → mã ngôn ngữ Google Translate
    private static readonly Dictionary<string, string> GoogleLangMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vi-VN"]  = "vi",
        ["en-US"]  = "en",
        ["en-GB"]  = "en",
        ["zh-Hans"]= "zh-CN",
        ["zh-Hant"]= "zh-TW",
        ["ja-JP"]  = "ja",
        ["ko-KR"]  = "ko",
        ["fr-FR"]  = "fr",
        ["de-DE"]  = "de",
        ["es-ES"]  = "es",
        ["th-TH"]  = "th",
        ["it-IT"]  = "it",
        ["pt-PT"]  = "pt",
        ["ru-RU"]  = "ru",
    };

    public TranslatorClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    /// <summary>
    /// Dịch text sang <paramref name="toLang"/>.
    /// Thử Azure trước (nếu có key), fallback sang Google Translate miễn phí.
    /// Trả null nếu cả hai đều thất bại.
    /// </summary>
    public async Task<string?> TryTranslateAsync(
        string text, string toLang, string? fromLang = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        // ── 1. Thử Azure Translator (nếu đã cấu hình key) ─────────────────
        var azureResult = await TryAzureAsync(text, toLang, fromLang, ct);
        if (azureResult != null) return azureResult;

        // ── 2. Fallback: Google Translate (miễn phí, không cần key) ────────
        return await TryGoogleAsync(text, toLang, fromLang ?? "vi-VN", ct);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Azure Cognitive Translator
    // ────────────────────────────────────────────────────────────────────────
    private async Task<string?> TryAzureAsync(
        string text, string toLang, string? fromLang, CancellationToken ct)
    {
        var endpoint = _config["Translator:Endpoint"] ?? "https://api.cognitive.microsofttranslator.com";
        var key      = _config["Translator:Key"];
        var region   = _config["Translator:Region"];

        if (string.IsNullOrWhiteSpace(key) || key == "YOUR_TRANSLATOR_KEY") return null;
        if (string.IsNullOrWhiteSpace(region)) return null;

        try
        {
            var url = $"{endpoint}/translate?api-version=3.0&to={Uri.EscapeDataString(toLang)}";
            if (!string.IsNullOrWhiteSpace(fromLang))
                url += $"&from={Uri.EscapeDataString(fromLang)}";

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Ocp-Apim-Subscription-Key", key);
            req.Headers.Add("Ocp-Apim-Subscription-Region", region);
            req.Content = JsonContent.Create(new[] { new AzureBody(text) });

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var data = await resp.Content.ReadFromJsonAsync<List<AzureResponse>>(cancellationToken: ct);
            return data?.FirstOrDefault()?.Translations?.FirstOrDefault()?.Text;
        }
        catch { return null; }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Google Translate (unofficial free endpoint — dùng cho dev/test)
    // Giới hạn ~5000 ký tự/request, không cần API key
    // ────────────────────────────────────────────────────────────────────────
    private async Task<string?> TryGoogleAsync(
        string text, string toLang, string fromLang, CancellationToken ct)
    {
        try
        {
            var to   = ToGoogleCode(toLang);
            var from = ToGoogleCode(fromLang);

            // Không cần dịch nếu cùng ngôn ngữ
            if (to == from) return text;

            var url = "https://translate.googleapis.com/translate_a/single"
                    + $"?client=gtx&sl={from}&tl={to}&dt=t"
                    + $"&q={Uri.EscapeDataString(text)}";

            var json = await _http.GetStringAsync(url, ct);

            // Response: [[["translated","original",...], ...], ...]
            using var doc = JsonDocument.Parse(json);
            var sb = new StringBuilder();
            foreach (var chunk in doc.RootElement[0].EnumerateArray())
            {
                var el = chunk[0];
                if (el.ValueKind == JsonValueKind.String)
                    sb.Append(el.GetString());
            }
            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch { return null; }
    }

    private static string ToGoogleCode(string bcp47Tag) =>
        GoogleLangMap.TryGetValue(bcp47Tag, out var code)
            ? code
            : bcp47Tag.Split('-')[0].ToLower(); // fallback: lấy phần đầu (vi-VN → vi)

    // ── Azure DTOs ───────────────────────────────────────────────────────────
    private sealed record AzureBody([property: JsonPropertyName("text")] string Text);

    private sealed class AzureResponse
    {
        [JsonPropertyName("translations")]
        public List<AzureItem>? Translations { get; set; }
    }

    private sealed class AzureItem
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}