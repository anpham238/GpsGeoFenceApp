using System.Net.Http.Json;
using System.Net.Http.Headers;

namespace MauiApp1.Services.Api;

public sealed class PlaybackApiClient
{
    private readonly HttpClient _http;

    public PlaybackApiClient(HttpClient http) => _http = http;

    public async Task LogAsync(
        int poiId,
        string triggerType,
        int? durationSeconds = null,
        bool success = true,
        CancellationToken ct = default)
    {
        try
        {
            var token = Preferences.Get("auth_token", "");
            if (string.IsNullOrWhiteSpace(token)) return;
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                PoiId = poiId,
                DurationSeconds = durationSeconds
            };
            using var resp = await _http.PostAsJsonAsync("/api/v1/profile/history", body, ct);
            if (!resp.IsSuccessStatusCode)
                System.Diagnostics.Debug.WriteLine($"[Playback] Log failed: {resp.StatusCode}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Playback] LogAsync error: {ex.Message}");
        }
    }
}
