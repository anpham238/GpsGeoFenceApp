using System.Net.Http.Json;
using Microsoft.Maui.Storage;

namespace MauiApp1.Services.Api;

public sealed class PlaybackApiClient
{
    private readonly HttpClient _http;

    public PlaybackApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task LogAsync(
        string poiId,
        string triggerType,      // ENTER / NEAR / TAP
        int? durationSeconds = null,
        bool success = true,
        CancellationToken ct = default)
    {
        try
        {
            var deviceId = GetOrCreateDeviceId();

            var body = new
            {
                DeviceId = deviceId,
                PoiId = poiId,
                TriggerType = triggerType,
                DurationListened = durationSeconds,
                IsSuccess = success
            };

            // MapApi: bạn sẽ tạo endpoint /api/v1/playback tương ứng (nếu chưa có)
            var resp = await _http.PostAsJsonAsync("/api/v1/playback", body, ct);
            // Không throw để tránh crash khi server tạm lỗi
            _ = resp.IsSuccessStatusCode;
        }
        catch
        {
            // offline hoặc timeout -> bỏ qua, đúng offline-first
        }
    }
    private static string GetOrCreateDeviceId()
    {
        const string key = "device_id";
        var id = Preferences.Get(key, "");
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString();
            Preferences.Set(key, id);
        }
        return id;
    }
}