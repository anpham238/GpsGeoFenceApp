using MauiApp1.Services.Api;
using Microsoft.AspNetCore.SignalR.Client;

namespace MauiApp1.Services.Guest;

public sealed class GuestHeartbeatService
{
    private readonly GuestDeviceService _device;
    private readonly GuestDeviceApiClient _api;
    private HubConnection? _hub;
    private double? _lastLat;
    private double? _lastLng;
    public GuestHeartbeatService(GuestDeviceService device, GuestDeviceApiClient api)
    {
        _device = device;
        _api = api;
    }

    public void Start()
    {
        _ = EnsureConnectedAsync();
    }

    public async Task StopAsync()
    {
        try
        {
            if (_hub is not null)
            {
                await _hub.StopAsync();
                await _hub.DisposeAsync();
            }
            _hub = null;
        }
        catch { }
    }

    public async Task ReportLocationAsync(double latitude, double longitude)
    {
        _lastLat = latitude;
        _lastLng = longitude;
        await EnsureConnectedAsync();
        if (_hub?.State == HubConnectionState.Connected)
            await _hub.InvokeAsync("SendLocation", await _device.GetOrCreateDeviceIdAsync(), latitude, longitude);
    }

    private async Task EnsureConnectedAsync()
    {
        try
        {
            var id = await _device.GetOrCreateDeviceIdAsync();
            if (_hub is null)
            {
                var url = $"{_api.BaseUrl}/hubs/device?deviceId={Uri.EscapeDataString(id)}" +
                          $"&platform={Uri.EscapeDataString(_device.GetPlatform())}" +
                          $"&appVersion={Uri.EscapeDataString(_device.GetAppVersion())}";
                _hub = new HubConnectionBuilder()
                    .WithUrl(url)
                    .WithAutomaticReconnect()
                    .Build();
            }

            if (_hub.State == HubConnectionState.Disconnected)
                await _hub.StartAsync();

            if (_hub.State == HubConnectionState.Connected && _lastLat.HasValue && _lastLng.HasValue)
                await _hub.InvokeAsync("SendLocation", id, _lastLat.Value, _lastLng.Value);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DeviceRealtime] {ex.Message}");
        }
    }
}
