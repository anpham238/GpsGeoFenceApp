using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MapApi.Services;

public sealed class TranslatorClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public TranslatorClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    // Không ném lỗi nếu thiếu key/region: trả null -> server fallback về baseText
    public async Task<string?> TryTranslateAsync(string text, string toLang, string? fromLang = null, CancellationToken ct = default)
    {
        var endpoint = _config["Translator:Endpoint"] ?? "https://api.cognitive.microsofttranslator.com";
        var key = _config["Translator:Key"];
        var region = _config["Translator:Region"];

        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(region))
            return null;

        // Text translation REST API: POST translate [3](https://developers.arcgis.com/net/maps-2d/tutorials/display-a-map-maui/)[4](https://www.w3tutorials.net/blog/how-to-auto-create-database-on-first-run/)
        var url = $"{endpoint}/translate?api-version=3.0&to={Uri.EscapeDataString(toLang)}";
        if (!string.IsNullOrWhiteSpace(fromLang))
            url += $"&from={Uri.EscapeDataString(fromLang)}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("Ocp-Apim-Subscription-Key", key);
        req.Headers.Add("Ocp-Apim-Subscription-Region", region);

        req.Content = JsonContent.Create(new[] { new TranslateBody(text) });

        using var resp = await _http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var data = await resp.Content.ReadFromJsonAsync<List<TranslateResponse>>(cancellationToken: ct);
        return data?.FirstOrDefault()?.Translations?.FirstOrDefault()?.Text;
    }

    private sealed record TranslateBody([property: JsonPropertyName("text")] string Text);

    private sealed class TranslateResponse
    {
        [JsonPropertyName("translations")]
        public List<TranslateItem>? Translations { get; set; }
    }

    private sealed class TranslateItem
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
