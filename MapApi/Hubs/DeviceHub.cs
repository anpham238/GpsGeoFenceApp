using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MapApi.Contracts.Realtime;
using MapApi.Services;

namespace MapApi.Hubs;

public class DeviceHub : Hub
{
    private readonly Data.AppDb _db;
    private readonly IDevicePresenceService _presence;
    public DeviceHub(Data.AppDb db, IDevicePresenceService presence)
    {
        _db = db;
        _presence = presence;
    }

    public override async Task OnConnectedAsync()
    {
        var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].ToString();
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = deviceId.Trim();
            var now = DateTime.UtcNow;
            var platform = Context.GetHttpContext()?.Request.Query["platform"].ToString();
            var appVersion = Context.GetHttpContext()?.Request.Query["appVersion"].ToString();

            _presence.MarkConnected(deviceId, Context.ConnectionId);

            var device = await _db.GuestDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device is null)
            {
                device = new Models.GuestDevice
                {
                    DeviceId = deviceId,
                    Platform = string.IsNullOrWhiteSpace(platform) ? null : platform.Trim(),
                    AppVersion = string.IsNullOrWhiteSpace(appVersion) ? null : appVersion.Trim(),
                    FirstSeenAt = now,
                    LastActiveAt = now
                };
                _db.GuestDevices.Add(device);
            }
            else
            {
                device.LastActiveAt = now;
                if (!string.IsNullOrWhiteSpace(platform)) device.Platform = platform.Trim();
                if (!string.IsNullOrWhiteSpace(appVersion)) device.AppVersion = appVersion.Trim();
            }
            await _db.SaveChangesAsync();

            var dto = new DevicePresenceDto
            {
                DeviceId = deviceId,
                IsOnline = true,
                LastActiveAt = now,
                OnlineCount = _presence.OnlineCount,
                TotalConnectionCount = _presence.TotalConnectionCount,
                FirstSeenAt = device.FirstSeenAt,
                Platform = device.Platform,
                AppVersion = device.AppVersion,
                LastLatitude = device.LastLatitude,
                LastLongitude = device.LastLongitude
            };

            await Clients.All.SendAsync(AdminDeviceEvents.PresenceChangedV1, new DevicePresenceChangedEnvelope
            {
                EmittedAt = now,
                Data = dto
            });
            await Clients.All.SendAsync("DeviceStatusChanged", dto);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var deviceId = Context.GetHttpContext()?.Request.Query["deviceId"].ToString();
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            deviceId = deviceId.Trim();
            var now = DateTime.UtcNow;
            var isStillOnline = _presence.MarkDisconnected(deviceId, Context.ConnectionId);

            var device = await _db.GuestDevices.FirstOrDefaultAsync(d => d.DeviceId == deviceId);
            if (device is not null)
            {
                device.LastActiveAt = now;
                await _db.SaveChangesAsync();
            }

            var disconnectDto = new DevicePresenceDto
            {
                DeviceId = deviceId,
                IsOnline = isStillOnline,
                LastActiveAt = now,
                OnlineCount = _presence.OnlineCount,
                TotalConnectionCount = _presence.TotalConnectionCount,
                FirstSeenAt = device?.FirstSeenAt ?? now,
                Platform = device?.Platform,
                AppVersion = device?.AppVersion,
                LastLatitude = device?.LastLatitude,
                LastLongitude = device?.LastLongitude
            };
            await Clients.All.SendAsync(AdminDeviceEvents.PresenceChangedV1, new DevicePresenceChangedEnvelope
            {
                EmittedAt = now,
                Data = disconnectDto
            });
            await Clients.All.SendAsync("DeviceStatusChanged", disconnectDto);
        }
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
