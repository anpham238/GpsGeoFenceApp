using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Maui.Storage;

namespace MauiApp1.Services.Api;

public sealed class ProfileApiClient
{
    private readonly HttpClient _http;

    public ProfileApiClient(HttpClient http) => _http = http;

    public string GetBaseUrl() => _http.BaseAddress?.ToString().TrimEnd('/') ?? "";

    private void AttachToken()
    {
        var token = Preferences.Get("auth_token", "");
        if (!string.IsNullOrEmpty(token))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<UserProfileDto?> GetMeAsync(CancellationToken ct = default)
    {
        AttachToken();
        try
        {
            return await _http.GetFromJsonAsync<UserProfileDto>("/api/v1/auth/me", ct);
        }
        catch { return null; }
    }

    public async Task<UserProfileDto?> UpdateProfileAsync(
        string? username, string? phone,
        Stream? avatarStream = null, string? avatarFileName = null,
        CancellationToken ct = default)
    {
        AttachToken();
        using var form = new MultipartFormDataContent();
        if (!string.IsNullOrWhiteSpace(username))
            form.Add(new StringContent(username), "Username");
        if (!string.IsNullOrWhiteSpace(phone))
            form.Add(new StringContent(phone), "PhoneNumber");
        if (avatarStream is not null && !string.IsNullOrWhiteSpace(avatarFileName))
            form.Add(new StreamContent(avatarStream), "Avatar", avatarFileName);

        var resp = await _http.PutAsync("/api/v1/profile", form, ct);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<UserProfileDto>(ct);
    }

    public async Task<List<HistoryPoiDto>> GetHistoryAsync(CancellationToken ct = default)
    {
        AttachToken();
        try
        {
            return await _http.GetFromJsonAsync<List<HistoryPoiDto>>("/api/v1/profile/history", ct)
                   ?? [];
        }
        catch { return []; }
    }

    public async Task<List<RoutePointDto>> GetTravelHistoryAsync(
        Guid sessionId, CancellationToken ct = default)
    {
        AttachToken();
        try
        {
            return await _http.GetFromJsonAsync<List<RoutePointDto>>(
                       $"/api/v1/profile/travel-history?sessionId={sessionId}", ct)
                   ?? [];
        }
        catch { return []; }
    }

    public async Task<DirectionsDto?> GetDirectionsAsync(
        int poiId, double? userLat = null, double? userLng = null, CancellationToken ct = default)
    {
        AttachToken();
        var url = $"/api/v1/pois/{poiId}/directions";
        if (userLat.HasValue && userLng.HasValue)
            url += $"?userLat={userLat.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                   $"&userLng={userLng.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var resp = await _http.GetAsync(url, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Forbidden) return null;
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<DirectionsDto>(ct);
    }

    public async Task<bool> UpgradeToProAsync(CancellationToken ct = default)
    {
        AttachToken();
        var resp = await _http.PostAsync("/api/v1/profile/upgrade", null, ct);
        if (!resp.IsSuccessStatusCode) return false;
        Preferences.Set("auth_plan_type", "PRO");
        return true;
    }
}

public sealed class UserProfileDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string Mail { get; set; } = "";
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string PlanType { get; set; } = "FREE";
    public DateTime? ProExpiryDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class HistoryPoiDto
{
    public long Id { get; set; }
    public int IdPoi { get; set; }
    public string PoiName { get; set; } = "";
    public int Quantity { get; set; }
    public DateTime LastVisitedAt { get; set; }
    public int? TotalDurationSeconds { get; set; }
}

public sealed class RoutePointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RecordedAt { get; set; }
}

public sealed class DirectionsDto
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = "";
    public DestinationPoint? Destination { get; set; }
    public double? DistanceMeters { get; set; }
    public double? DurationSeconds { get; set; }
    public List<RouteCoord>? RouteCoordinates { get; set; }
    public string? Message { get; set; }
}
public sealed class DestinationPoint { public double Lat { get; set; } public double Lng { get; set; } }
public sealed class RouteCoord { public double Lat { get; set; } public double Lng { get; set; } }
