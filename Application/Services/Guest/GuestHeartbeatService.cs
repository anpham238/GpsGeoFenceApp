using MauiApp1.Services.Api;

namespace MauiApp1.Services.Guest;

public sealed class GuestHeartbeatService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);
    private const double MinMoveMeters = 25;
    private readonly GuestDeviceService _device;
    private readonly GuestDeviceApiClient _api;
    private CancellationTokenSource? _cts;
    private double? _lastLat;
    private double? _lastLng;
    public GuestHeartbeatService(GuestDeviceService device, GuestDeviceApiClient api)
    {
        _device = device;
        _api = api;
    }

    public void Start()
    {
        if (_cts is not null) return;
        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        var cts = _cts;
        _cts = null;
        cts?.Cancel();

        try
        {
            var id = await _device.GetOrCreateDeviceIdAsync();
            await _api.SignalOfflineAsync(id);
        }
        catch { }
    }

    public async Task ReportLocationAsync(double latitude, double longitude)
    {
        if (_lastLat.HasValue && _lastLng.HasValue &&
            HaversineMeters(_lastLat.Value, _lastLng.Value, latitude, longitude) < MinMoveMeters)
            return;

        _lastLat = latitude;
        _lastLng = longitude;
        await PingAsync(latitude, longitude);
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await PingAsync(_lastLat, _lastLng);
            try { await Task.Delay(Interval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task PingAsync(double? lat, double? lng)
    {
        try
        {
            var id = await _device.GetOrCreateDeviceIdAsync();
            await _api.SendHeartbeatAsync(id, _device.GetPlatform(), _device.GetAppVersion(), lat, lng);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GuestHeartbeat] {ex.Message}");
        }
    }

    private static double HaversineMeters(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLng = (lng2 - lng1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                   Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
