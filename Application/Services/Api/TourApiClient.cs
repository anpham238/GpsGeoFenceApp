using System.Net.Http.Json;
using MauiApp1.Models;

namespace MauiApp1.Services.Api;

public sealed class TourApiClient
{
    private readonly HttpClient _http;
    public TourApiClient(HttpClient http) => _http = http;

    public async Task<List<TourDto>> GetAllAsync(CancellationToken ct = default)
    {
        try
        {
            var data = await _http.GetFromJsonAsync<List<TourDto>>("/api/v1/sync/tours", ct);
            return data ?? [];
        }
        catch { return []; }
    }
}
