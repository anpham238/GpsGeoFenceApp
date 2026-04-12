using System.Net.Http.Json;
using MauiApp1.Models;

namespace MauiApp1.Services.Api;

public sealed class PoiNarrationApiClient(HttpClient http)
{
    public async Task<PoiNarrationDto?> GetNarrationAsync(
        int poiId, string lang, string eventType, CancellationToken ct = default)
    {
        var url = $"/api/v1/pois/{poiId}/narration?lang={Uri.EscapeDataString(lang)}&eventType={Uri.EscapeDataString(eventType)}";
        return await http.GetFromJsonAsync<PoiNarrationDto>(url, ct);
    }
}
