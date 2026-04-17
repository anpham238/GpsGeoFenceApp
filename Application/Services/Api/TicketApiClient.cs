using System.Net.Http.Json;

namespace MauiApp1.Services.Api;

public sealed class TicketApiClient(HttpClient http)
{
    public async Task<TicketScanResult?> ScanTicketAsync(string ticketCode, CancellationToken ct = default)
    {
        var resp = await http.PostAsync($"/api/v1/tickets/scan/{ticketCode}", null, ct); if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<TicketScanResult>(ct);
    }
}

public class TicketScanResult
{
    public int PoiId { get; set; }
    public string Language { get; set; } = "";
    public int Remaining { get; set; }
}