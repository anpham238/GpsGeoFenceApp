using System.Net.Http.Json;
using Microsoft.Maui.Storage;

namespace MauiApp1.Services.Api;

public sealed class UsageApiClient(HttpClient http)
{
    // Trả về: (allowed, resetInHours) — resetInHours > 0 nếu bị từ chối
    public async Task<(bool allowed, double resetInHours)> CheckAsync(
        string entityId, string actionType, CancellationToken ct = default)
    {
        try
        {
            var resp = await http.PostAsJsonAsync(
                "/api/v1/usage/check",
                new { entityId, actionType }, ct);

            if ((int)resp.StatusCode == 402)
            {
                var body = await resp.Content.ReadFromJsonAsync<UsageResultDto>(ct);
                return (false, body?.ResetInHours ?? 24);
            }
            return (resp.IsSuccessStatusCode, 0);
        }
        catch
        {
            return (true, 0); // Lỗi mạng → không chặn user
        }
    }

    public async Task<UsageStatusDto?> GetStatusAsync(
        string entityId, string actionType, CancellationToken ct = default)
    {
        try
        {
            var url = $"/api/v1/usage/status?entityId={Uri.EscapeDataString(entityId)}&actionType={Uri.EscapeDataString(actionType)}";
            return await http.GetFromJsonAsync<UsageStatusDto>(url, ct);
        }
        catch
        {
            return null; // Trả về null nếu lỗi mạng
        }
    }

    public async Task<AccessCheckResultDto> CheckPoiAccessAsync(
        int poiId, string? deviceId = null, CancellationToken ct = default)
    {
        try
        {
            var token = Preferences.Get("auth_token", "");
            if (!string.IsNullOrEmpty(token))
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var resp = await http.PostAsJsonAsync(
                "/api/access/check-poi",
                new { poiId, deviceId }, ct);

            if ((int)resp.StatusCode == 402)
            {
                var body = await resp.Content.ReadFromJsonAsync<AccessCheckResultDto>(ct);
                return body ?? new AccessCheckResultDto { AccessGranted = false, ShowPaywall = true, PoiId = poiId };
            }
            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadFromJsonAsync<AccessCheckResultDto>(ct)
                    ?? new AccessCheckResultDto { AccessGranted = true, PoiId = poiId };

            return new AccessCheckResultDto { AccessGranted = true, PoiId = poiId };
        }
        catch
        {
            return new AccessCheckResultDto { AccessGranted = true, PoiId = poiId };
        }
    }

    public async Task ConsumePoiListenAsync(
        int poiId, int? areaId = null, string? deviceId = null, CancellationToken ct = default)
    {
        try
        {
            var token = Preferences.Get("auth_token", "");
            if (!string.IsNullOrEmpty(token))
                http.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            await http.PostAsJsonAsync(
                "/api/usage/consume-poi-listen",
                new { poiId, areaId, deviceId }, ct);
        }
        catch { /* fire-and-forget, không chặn user */ }
    }

    // Lấy entityId: UserId nếu đăng nhập, DeviceId nếu là Guest
    public static string GetEntityId()
    {
        if (AuthApiClient.IsLoggedIn())
            return AuthApiClient.GetCurrentUserId().ToString();

        var deviceId = Preferences.Get("guest_device_id", "");
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = Guid.NewGuid().ToString("N");
            Preferences.Set("guest_device_id", deviceId);
        }
        return deviceId;
    }
}

file sealed class UsageResultDto
{
    public bool Allowed { get; set; }
    public double ResetInHours { get; set; }
    public string? Message { get; set; }
}

public sealed class UsageStatusDto
{
    public bool Allowed { get; set; }
    public int Used { get; set; }
    public int Limit { get; set; }
    public double ResetInHours { get; set; }
}
