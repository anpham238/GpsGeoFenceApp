using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;

namespace MauiApp1.Services.Api;

/// <summary>
/// Wrapper gọi backend TranslatorClient
/// </summary>
public sealed class TranslatorClient
{
    private readonly HttpClient _http;

    public TranslatorClient(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Gọi /api/translator/translate endpoint từ backend
    /// </summary>
    public async Task<string?> TryTranslateAsync(
        string text,
        string toLang,
        string? fromLang = null,
        CancellationToken ct = default)
    {
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/translator/translate");
            req.Content = JsonContent.Create(new
            {
                text,
                toLang,
                fromLang
            });

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var result = await resp.Content.ReadAsStringAsync(ct);
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TranslatorClient] Error: {ex.Message}");
            return null;
        }
    }
}       