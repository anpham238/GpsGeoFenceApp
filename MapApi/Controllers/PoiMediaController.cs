using MapApi.Data;
using MapApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Controllers;

public sealed record SetMapLinkRequest(string MapLink);
public sealed record SetAudioUrlRequest(string AudioUrl);
public sealed record ReorderImagesRequest(List<long> OrderedIds);
public sealed record AddImageUrlRequest(string ImageUrl);

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

    [HttpPost("audio")]
    public async Task<IActionResult> SetAudioUrl(int id, [FromBody] SetAudioUrlRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.AudioUrl)) return BadRequest("AudioUrl is required");
        var lang = await _db.PoiLanguages.FirstOrDefaultAsync(l => l.IdPoi == id && l.LanguageTag == "vi-VN", ct);
        if (lang is null) return NotFound("PoiLanguage vi-VN not found for this POI.");
        lang.ProAudioUrl = req.AudioUrl.Trim();
        await _db.SaveChangesAsync(ct);
        return Ok(new { poiId = id, audioUrl = lang.ProAudioUrl });
    }

    [HttpPost("audio/upload")]
    public async Task<IActionResult> UploadAudio(int id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".mp3" and not ".wav") return BadRequest("Only .mp3/.wav");
        if (file.Length > 50 * 1024 * 1024) return BadRequest("File too large (max 50 MB)");

        var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
        if (poi is null) return NotFound("Không tìm thấy địa điểm này.");

        var dir = Path.Combine(_env.WebRootPath, "audio");
        Directory.CreateDirectory(dir);
        var safeName = $"{id}_{Guid.NewGuid():N}{ext}";
        await using var fs = System.IO.File.Create(Path.Combine(dir, safeName));
        await file.CopyToAsync(fs, ct);
        var audioUrl = $"/audio/{safeName}";

        var lang = await _db.PoiLanguages.FirstOrDefaultAsync(l => l.IdPoi == id && l.LanguageTag == "vi-VN", ct);
        if (lang is not null)
        {
            lang.ProAudioUrl = audioUrl;
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new { poiId = id, audioUrl });
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

    // ── Multi-image gallery ──────────────────────────────────────────────

    [HttpGet("images")]
    public async Task<IActionResult> GetImages(int id, CancellationToken ct)
    {
        var images = await _db.PoiImages
            .Where(x => x.IdPoi == id)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.CreatedAt)
            .Select(x => new { x.Id, x.ImageUrl, x.SortOrder })
            .ToListAsync(ct);
        return Ok(images);
    }

    [HttpPost("images/url")]
    public async Task<IActionResult> AddImageByUrl(int id, [FromBody] AddImageUrlRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ImageUrl)) return BadRequest("ImageUrl is required");
        if (!Uri.TryCreate(req.ImageUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
            return BadRequest("ImageUrl must be a valid http/https URL");

        var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
        if (poi is null) return NotFound("Không tìm thấy địa điểm này.");

        var nextOrder = await _db.PoiImages
            .Where(x => x.IdPoi == id)
            .MaxAsync(x => (int?)x.SortOrder, ct) ?? -1;

        var entity = new PoiImage { IdPoi = id, ImageUrl = req.ImageUrl.Trim(), SortOrder = nextOrder + 1 };
        _db.PoiImages.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(new { entity.Id, entity.ImageUrl, entity.SortOrder });
    }

    [HttpPost("images")]
    public async Task<IActionResult> AddImage(int id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".jpg" and not ".jpeg" and not ".png") return BadRequest("Only .jpg/.png");
        if (file.Length > 10 * 1024 * 1024) return BadRequest("File too large (max 10 MB)");

        var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
        if (poi is null) return NotFound("Không tìm thấy địa điểm này.");

        var dir = Path.Combine(_env.WebRootPath, "images");
        Directory.CreateDirectory(dir);
        var safeName = $"{id}_{Guid.NewGuid():N}{ext}";
        await using var fs = System.IO.File.Create(Path.Combine(dir, safeName));
        await file.CopyToAsync(fs, ct);

        var nextOrder = await _db.PoiImages
            .Where(x => x.IdPoi == id)
            .MaxAsync(x => (int?)x.SortOrder, ct) ?? -1;

        var entity = new PoiImage { IdPoi = id, ImageUrl = $"/images/{safeName}", SortOrder = nextOrder + 1 };
        _db.PoiImages.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(new { entity.Id, entity.ImageUrl, entity.SortOrder });
    }

    [HttpDelete("images/{imageId:long}")]
    public async Task<IActionResult> DeleteImage(int id, long imageId, CancellationToken ct)
    {
        var img = await _db.PoiImages.FirstOrDefaultAsync(x => x.Id == imageId && x.IdPoi == id, ct);
        if (img is null) return NotFound();

        if (!img.ImageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            var filePath = Path.Combine(_env.WebRootPath, img.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);
        }

        _db.PoiImages.Remove(img);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("images/reorder")]
    public async Task<IActionResult> ReorderImages(int id, [FromBody] ReorderImagesRequest req, CancellationToken ct)
    {
        var images = await _db.PoiImages.Where(x => x.IdPoi == id).ToListAsync(ct);
        for (var i = 0; i < req.OrderedIds.Count; i++)
        {
            var img = images.FirstOrDefault(x => x.Id == req.OrderedIds[i]);
            if (img is not null) img.SortOrder = i;
        }
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
