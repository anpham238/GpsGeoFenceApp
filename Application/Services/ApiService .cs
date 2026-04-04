using System.Net.Http.Json;
using System.Text.Json;
using MauiApp1.Data;
using MauiApp1.Models;
using Microsoft.Maui.Storage;

namespace MauiApp1.Services;

/// <summary>
/// Goi Backend API (MapApi) de:
///  - Lay / dong bo POI tu SQL Server vao SQLite local
///  - Ghi PlaybackLog len server sau khi phat thuyet minh
///
/// URL:
///  - Emulator Android -> 10.0.2.2 (localhost cua may tinh)
///  - Thiet bi that  -> IP lan cua may tinh (VD: 192.168.1.x:5000)
///  - Production     -> domain thuc (https://...)
/// </summary>
public sealed class ApiService
{
    // ── Doi URL khi deploy ───────────────────────────────────────────
#if DEBUG
    // Emulator -> 10.0.2.2 = localhost cua may chu Windows
    private const string BaseUrl = "http://10.0.2.2:5000";
#else
    private const string BaseUrl = "https://your-api-domain.com";
#endif

    private readonly HttpClient _http;
    private readonly PoiDatabase _db;
    private readonly JsonSerializerOptions _json = new()
    { PropertyNameCaseInsensitive = true };

    // Key luu thoi diem sync cuoi cung
    private const string LastSyncKey = "api_last_sync";

    public ApiService(PoiDatabase db)
    {
        _db = db;
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    // ════════════════════════════════════════════════════════════════
    // SYNC POI: API → SQLite
    // Goi khi app khoi dong (co mang). MapPage dung SQLite lam nguon chinh.
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lay POI moi tu API va luu vao SQLite.
    /// - Lan dau: lay tat ca (since=null)
    /// - Lan sau: chi lay POI co UpdatedAt > lan sync truoc
    /// </summary>
    public async Task SyncPoisAsync()
    {
        try
        {
            // Lay moc thoi gian sync truoc
            var lastSync = Preferences.Get(LastSyncKey, "");
            var url = string.IsNullOrEmpty(lastSync)
                ? "/api/v1/pois"
                : $"/api/v1/pois/sync?since={Uri.EscapeDataString(lastSync)}";

            System.Diagnostics.Debug.WriteLine($"[API] Sync POI: {url}");

            var resp = await _http.GetAsync(url);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();

            List<ApiPoi>? items;

            // GET /api/v1/pois   tra ve mang truc tiep
            // GET /api/v1/pois/sync tra ve { Items: [...], ServerTime: ... }
            if (url.Contains("sync"))
            {
                var wrapper = JsonSerializer.Deserialize<SyncResponse>(json, _json);
                items = wrapper?.Items;
            }
            else
            {
                items = JsonSerializer.Deserialize<List<ApiPoi>>(json, _json);
            }

            if (items == null || items.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[API] Khong co POI moi.");
                return;
            }

            // Luu vao SQLite (upsert)
            int saved = 0;
            foreach (var api in items)
            {
                var poi = ToMauiModel(api);
                await _db.SaveAsync(poi);
                saved++;
            }

            // Cap nhat moc thoi gian
            Preferences.Set(LastSyncKey, DateTime.UtcNow.ToString("o"));
            System.Diagnostics.Debug.WriteLine($"[API] Sync xong: {saved} POI.");
        }
        catch (HttpRequestException ex)
        {
            // Mat mang hoac API chua chay -> bo qua, dung SQLite cache
            System.Diagnostics.Debug.WriteLine($"[API] Khong ket noi duoc: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[API] Sync loi: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════
    // LOG PLAYBACK: MAUI → API
    // Goi bat dong bo sau khi NarrationManager phat xong
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Ghi log phat thuyet minh len server.
    /// Goi fire-and-forget, khong block UI.
    /// </summary>
    public async Task LogPlaybackAsync(
        string poiId,
        string triggerType,            // ENTER / NEAR / TAP
        int? durationSeconds = null,
        bool success = true)
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
            var resp = await _http.PostAsJsonAsync("/api/v1/playback", body);
            System.Diagnostics.Debug.WriteLine(
                resp.IsSuccessStatusCode
                    ? $"[API] Log OK: {poiId} / {triggerType}"
                    : $"[API] Log loi {resp.StatusCode}: {poiId}");
        }
        catch (Exception ex)
        {
            // Khong crash app neu mat mang
            System.Diagnostics.Debug.WriteLine($"[API] Log exception: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════
    // HELPERS
    // ════════════════════════════════════════════════════════════════

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

    private static Poi ToMauiModel(ApiPoi a) => new()
    {
        Id = a.Id,
        Name = a.Name,
        Description = a.Description ?? "",
        Latitude = a.Latitude,
        Longitude = a.Longitude,
        RadiusMeters = a.RadiusMeters,
        NearRadiusMeters = a.NearRadiusMeters,
        DebounceSeconds = a.DebounceSeconds,
        CooldownSeconds = a.CooldownSeconds,
        Priority = a.Priority,
        NarrationText = a.NarrationText,
        AudioUrl = a.AudioUrl,
        ImageUrl = a.ImageUrl,
        MapLink = a.MapLink,
        IsActive = a.IsActive,
        // CreatedAt khong co trong API response -> dung UtcNow
    };

    // ── DTO map voi JSON tra ve tu API ────────────────────────────────
    private record ApiPoi(
        string Id,
        string Name,
        string? Description,
        double Latitude,
        double Longitude,
        float RadiusMeters,
        float NearRadiusMeters,
        int DebounceSeconds,
        int CooldownSeconds,
        int Priority,
        string? Language,
        string? NarrationText,
        string? AudioUrl,
        string? ImageUrl,
        string? MapLink,
        bool IsActive,
        DateTime UpdatedAt
    );

    private record SyncResponse(List<ApiPoi> Items, DateTime ServerTime);
}