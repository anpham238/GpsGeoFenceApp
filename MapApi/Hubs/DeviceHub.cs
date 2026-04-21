using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Hubs;

public class DeviceHub : Hub
{
    private readonly Data.AppDb _db;
    public DeviceHub(Data.AppDb db) { _db = db; }

    public override async Task OnConnectedAsync()
    {
        var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].ToString();
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            var device = await _db.GuestDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device != null)
            {
                device.LastActiveAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
            await Clients.Others.SendAsync("DeviceConnected", deviceId);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].ToString();
        if (!string.IsNullOrWhiteSpace(deviceId))
            await Clients.Others.SendAsync("DeviceDisconnected", deviceId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task SendLocation(string deviceId, double lat, double lng)
    {
        var device = await _db.GuestDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
        if (device != null)
        {
            device.LastLatitude = lat;
            device.LastLongitude = lng;
            device.LastActiveAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
