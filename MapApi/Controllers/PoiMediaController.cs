using MapApi.Data;
using MapApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace MapApi.Controllers;

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
    public async Task<IActionResult> UploadImage(string id, IFormFile file, CancellationToken ct)
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

        // TỐI ƯU: Lưu vào bảng PoiMedia thay vì bảng Pois
        var newMedia = new PoiMedia
        {
            PoiId = id,
            Url = fileUrl,
            MediaType = 1, // Giả sử 1 là Image
            MimeType = file.ContentType,
            IsPrimary = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.PoiMedia.Add(newMedia);
        await _db.SaveChangesAsync(ct);

        return Ok(new { poiId = id, imageUrl = fileUrl });
    }

    // Bạn có thể giữ lại hàm UploadAudio và sửa tương tự như UploadImage (Thay MediaType = 2)
}