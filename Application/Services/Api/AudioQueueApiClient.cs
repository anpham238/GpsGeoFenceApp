using System.Net.Http.Json;

namespace MauiApp1.Services.Api;

public sealed class AudioQueueApiClient
{
    private readonly HttpClient _http;

    public AudioQueueApiClient(HttpClient http) => _http = http;

    public Task StartAsync(string deviceId, int poiId, string? visitorLabel, string sessionId, CancellationToken ct = default) =>
        PostSilentAsync("/api/v1/audio-sessions/start", deviceId, poiId, visitorLabel, sessionId, ct);

    public Task EndAsync(string deviceId, int poiId, string? visitorLabel, string sessionId, CancellationToken ct = default) =>
        PostSilentAsync("/api/v1/audio-sessions/end", deviceId, poiId, visitorLabel, sessionId, ct);

    private async Task PostSilentAsync(string url, string deviceId, int poiId, string? visitorLabel, string sessionId, CancellationToken ct)
    {
        try
        {
            await _http.PostAsJsonAsync(url, new
            {
                DeviceId = deviceId,
                PoiId = poiId,
                VisitorLabel = visitorLabel,
                SessionId = sessionId
            }, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioQueue] {ex.Message}");
        }
    }
}
