using System.Net.Http.Json;
using MauiApp1.Models;

namespace MauiApp1.Services.Api;

public sealed class PoiApiClient
{
    private readonly HttpClient _http;

    public PoiApiClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<PoiDto>> GetAllAsync(CancellationToken ct = default)
    {
        // MapApi endpoint: /api/v1/pois [3](https://communitytoolkit.github.io/Datasync/in-depth/client/)
        var data = await _http.GetFromJsonAsync<List<PoiDto>>("/api/v1/pois", ct);
        return data ?? new List<PoiDto>();
    }

    // (Tuỳ chọn) Push nếu bạn cho sửa POI trên app:
    public async Task PutAsync(string id, PoiDto dto, CancellationToken ct = default)
    {
        // MapApi: PUT /api/v1/pois/{id} [3](https://communitytoolkit.github.io/Datasync/in-depth/client/)
        var res = await _http.PutAsJsonAsync($"/api/v1/pois/{id}", dto, ct);
        res.EnsureSuccessStatusCode();
    }
}