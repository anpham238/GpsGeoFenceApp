using System.Net.Http.Json;

namespace MauiApp1.Services.Api;

public sealed class AnalyticsClient
{
    private readonly HttpClient _http;
    public readonly Guid SessionId = Guid.NewGuid();

    public AnalyticsClient(HttpClient http) => _http = http;

    public Task LogVisitAsync(int poiId, string action) =>
        PostSilentAsync("/api/v1/analytics/visit",
            new { sessionId = SessionId, poiId, action });

    public Task LogRouteAsync(double lat, double lng) =>
        PostSilentAsync("/api/v1/analytics/route",
            new { sessionId = SessionId, latitude = lat, longitude = lng });

    public Task LogListenDurationAsync(int poiId, int durationSeconds) =>
        PostSilentAsync("/api/v1/analytics/listen-duration",
            new { sessionId = SessionId, poiId, durationSeconds });

    private async Task PostSilentAsync(string url, object body)
    {
        try { await _http.PostAsJsonAsync(url, body); }
        catch { /* không để lỗi analytics crash app */ }
    }
}
