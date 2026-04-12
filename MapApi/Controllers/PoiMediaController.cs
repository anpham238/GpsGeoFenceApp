using MapApi.Data;
using MapApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Controllers;

public sealed record SetMapLinkRequest(string MapLink);
public sealed record SetAudioRequest(string AudioUrl);

[ApiController]
[Route("api/v1/pois/{id}")]
public class PoiMediaController : ControllerBase
{
    private readonly AppDb _db;
    private readonly IWebHostEnvironment _env;

    public PoiMediaController(AppDb db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(int id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext is not ".jpg" and not ".jpeg" and not ".png") return BadRequest("Only .jpg/.png");
        if (file.Length > 10 * 1024 * 1024) return BadRequest("File too large");

        var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
        if (poi is null) return NotFound("Không tìm thấy địa điểm này.");

        var dir = Path.Combine(_env.WebRootPath, "images");
        Directory.CreateDirectory(dir);

        var safeName = $"{id}_{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(dir, safeName);

        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs, ct);

        var fileUrl = $"/images/{safeName}";

        var existing = await _db.PoiMedia.FirstOrDefaultAsync(m => m.IdPoi == id, ct);
        if (existing is not null)
            existing.Image = fileUrl;
        else
            _db.PoiMedia.Add(new PoiMedia { IdPoi = id, Image = fileUrl });

        await _db.SaveChangesAsync(ct);
        return Ok(new { poiId = id, imageUrl = fileUrl });
    }

    [HttpPost("maplink")]
    public async Task<IActionResult> SetMapLink(int id, [FromBody] SetMapLinkRequest req, CancellationToken ct)
    {
        var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
        if (poi is null) return NotFound("Không tìm thấy địa điểm này.");

        var existing = await _db.PoiMedia.FirstOrDefaultAsync(m => m.IdPoi == id, ct);
        if (existing is not null)
            existing.MapLink = req.MapLink;
        else
            _db.PoiMedia.Add(new PoiMedia { IdPoi = id, MapLink = req.MapLink });

        await _db.SaveChangesAsync(ct);
        return Ok(new { poiId = id, mapLink = req.MapLink });
    }

    [HttpPost("audio")]
    public async Task<IActionResult> SetAudio(int id, [FromBody] SetAudioRequest req, CancellationToken ct)
    {
        var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
        if (poi is null) return NotFound("Không tìm thấy địa điểm này.");

        var existing = await _db.PoiMedia.FirstOrDefaultAsync(m => m.IdPoi == id, ct);
        if (existing is not null)
            existing.Audio = req.AudioUrl;
        else
            _db.PoiMedia.Add(new PoiMedia { IdPoi = id, Audio = req.AudioUrl });

        await _db.SaveChangesAsync(ct);
        return Ok(new { poiId = id, audioUrl = req.AudioUrl });
    }
}
