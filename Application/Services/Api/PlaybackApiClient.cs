using System.Net.Http.Json;

namespace MauiApp1.Services.Api;

public sealed class PlaybackApiClient
{
    private readonly HttpClient _http;

    public PlaybackApiClient(HttpClient http) => _http = http;

    public async Task LogAsync(
        string poiId,
        string triggerType,
        int? durationSeconds = null,
        bool success = true,
        CancellationToken ct = default)
    {
        try
        {
            var userId = AuthApiClient.GetCurrentUserId();
            if (userId == Guid.Empty) return;

            var body = new
            {
                PoiId = poiId,
                UserId = userId,
                DurationSeconds = durationSeconds
            };
            using var resp = await _http.PostAsJsonAsync("/api/v1/history", body, ct);
            _ = resp.IsSuccessStatusCode;
        }
        catch
        {
            // offline / timeout → bỏ qua (offline-first)
        }
    }
}
