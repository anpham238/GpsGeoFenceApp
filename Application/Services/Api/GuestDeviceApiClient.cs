using System.Net.Http.Json;

namespace MauiApp1.Services.Api;

public sealed class GuestDeviceApiClient
{
    private readonly HttpClient _http;

    public GuestDeviceApiClient(HttpClient http) => _http = http;

    public async Task<bool> SendHeartbeatAsync(
        string deviceId,
        string? platform,
        string? appVersion,
        double? latitude,
        double? longitude,
        CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/guest-devices/heartbeat", new
            {
                DeviceId = deviceId,
                Platform = platform,
                AppVersion = appVersion,
                Latitude = latitude,
                Longitude = longitude
            }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SignalOfflineAsync(string deviceId, CancellationToken ct = default)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("/api/v1/guest-devices/offline",
                new { DeviceId = deviceId }, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
