using System.Net.Http.Json;

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
                return (false, body?.ResetInHours ?? 12);
            }
            return (resp.IsSuccessStatusCode, 0);
        }
        catch
        {
            return (true, 0); // Lỗi mạng → không chặn user
        }
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
