using MapApi.Data;
using MapApi.Models;
using MapApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Controllers;

[ApiController]
[Route("api/v1/guest-devices")]
public class GuestDevicesController : ControllerBase
{
    private readonly AppDb _db;
    private readonly IDevicePresenceService _presence;
    public GuestDevicesController(AppDb db, IDevicePresenceService presence)
    {
        _db = db;
        _presence = presence;
    }

    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] GuestHeartbeatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceId))
            return BadRequest(new { error = "DeviceId là bắt buộc" });

        var deviceId = req.DeviceId.Trim();
        var now = DateTime.UtcNow;

        var device = await _db.GuestDevices.FirstOrDefaultAsync(x => x.DeviceId == deviceId, ct);
        if (device is null)
        {
            device = new GuestDevice
            {
                DeviceId = deviceId,
                Platform = req.Platform,
                AppVersion = req.AppVersion,
                LastLatitude = req.Latitude,
                LastLongitude = req.Longitude,
                FirstSeenAt = now,
                LastActiveAt = now
            };
            _db.GuestDevices.Add(device);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(req.Platform))   device.Platform = req.Platform;
            if (!string.IsNullOrWhiteSpace(req.AppVersion)) device.AppVersion = req.AppVersion;
            if (req.Latitude.HasValue)                      device.LastLatitude = req.Latitude;
            if (req.Longitude.HasValue)                     device.LastLongitude = req.Longitude;
            device.LastActiveAt = now;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true, device.DeviceId, device.LastActiveAt });
    }

    [HttpPost("offline")]
    public async Task<IActionResult> MarkOffline([FromBody] GuestOfflineRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DeviceId))
            return BadRequest(new { error = "DeviceId là bắt buộc" });
        var device = await _db.GuestDevices.FirstOrDefaultAsync(x => x.DeviceId == req.DeviceId, ct);
        if (device is null) return Ok(new { ok = true });
        device.LastActiveAt = DateTime.UtcNow.AddSeconds(-100);
        await _db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }
    [HttpGet("online")]
    public async Task<IActionResult> GetOnline(CancellationToken ct)
    {
        var list = await _db.GuestDevices.AsNoTracking()
            .Where(x => _presence.IsOnline(x.DeviceId))
            .OrderByDescending(x => x.LastActiveAt)
            .Select(x => new
            {
                x.DeviceId, x.Platform, x.AppVersion,
                x.LastLatitude, x.LastLongitude,
                x.FirstSeenAt, x.LastActiveAt
            })
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var list = await _db.GuestDevices.AsNoTracking()
            .OrderByDescending(x => x.LastActiveAt)
            .Select(x => new
            {
                x.DeviceId, x.Platform, x.AppVersion,
                x.LastLatitude, x.LastLongitude,
                x.FirstSeenAt, x.LastActiveAt,
                IsOnline = _presence.IsOnline(x.DeviceId)
            })
            .ToListAsync(ct);
        return Ok(list);
    }
}

public sealed class GuestHeartbeatRequest
{
    public string DeviceId { get; set; } = "";
    public string? Platform { get; set; }
    public string? AppVersion { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public sealed record GuestOfflineRequest(string DeviceId);
