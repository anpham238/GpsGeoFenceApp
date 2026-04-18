using System.Net.Http.Json;
using MauiApp1.Models;

namespace MauiApp1.Services.Api;

public sealed class PoiApiClient
{
    private readonly HttpClient _http;
    public string BaseUrl => _http.BaseAddress?.ToString() ?? "http://192.168.31.212:5150";

    public PoiApiClient(HttpClient http) => _http = http;

    public async Task<List<PoiDto>> GetAllAsync(string? lang = null, CancellationToken ct = default)
    {
        var url = string.IsNullOrWhiteSpace(lang)
            ? "/api/v1/pois"
            : $"/api/v1/pois?lang={Uri.EscapeDataString(lang)}";

        var data = await _http.GetFromJsonAsync<List<PoiDto>>(url, ct);
        return data ?? [];
    }

    public async Task PutAsync(string id, PoiDto dto, CancellationToken ct = default)
    {
        var res = await _http.PutAsJsonAsync($"/api/v1/pois/{Uri.EscapeDataString(id)}", dto, ct);
        res.EnsureSuccessStatusCode();
    }

    public async Task<string?> GetServerVersionAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await _http.GetFromJsonAsync<SyncVersionDto>("/api/v1/sync/version", ct);
            return result?.Version;
        }
        catch { return null; }
    }

    public async Task<List<string>> GetImagesAsync(int poiId, CancellationToken ct = default)
    {
        try
        {
            var data = await _http.GetFromJsonAsync<List<PoiImageItemDto>>($"/api/v1/pois/{poiId}/images", ct);
            return data?.Select(x => x.ImageUrl).ToList() ?? [];
        }
        catch { return []; }
    }

    private sealed record SyncVersionDto(string Version, int Count);
    private sealed record PoiImageItemDto(long Id, string ImageUrl, int SortOrder);
}
