using MapApi.Data;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/pois/{id}")]
public class PoiMediaController : ControllerBase
{
    private readonly AppDb _db;
    private readonly IWebHostEnvironment _env;

    public PoiMediaController(AppDb db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    [HttpPost("audio")]
    public async Task<IActionResult> UploadAudio(string id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        // chỉ cho phép mp3/wav (whitelist)  [7](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0)
        if (ext is not ".mp3" and not ".wav") return BadRequest("Only .mp3/.wav");

        // giới hạn kích thước (ví dụ 20MB)  [7](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0)
        if (file.Length > 20 * 1024 * 1024) return BadRequest("File too large");

        var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
        if (poi is null) return NotFound();

        var dir = Path.Combine(_env.WebRootPath, "audio");
        Directory.CreateDirectory(dir);

        // tên an toàn (không dùng trực tiếp file.FileName)  [7](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0)
        var safeName = $"{id}_{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(dir, safeName);

        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs, ct);

        poi.AudioUrl = $"/audio/{safeName}";
        await _db.SaveChangesAsync(ct);

        return Ok(new { poi.Id, poi.AudioUrl });
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(string id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext is not ".jpg" and not ".jpeg" and not ".png") return BadRequest("Only .jpg/.png");
        if (file.Length > 10 * 1024 * 1024) return BadRequest("File too large");

        var poi = await _db.Pois.FindAsync(new object[] { id }, ct);
        if (poi is null) return NotFound();

        var dir = Path.Combine(_env.WebRootPath, "images");
        Directory.CreateDirectory(dir);

        var safeName = $"{id}_{Guid.NewGuid():N}{ext}";
        var path = Path.Combine(dir, safeName);

        await using var fs = System.IO.File.Create(path);
        await file.CopyToAsync(fs, ct);

        poi.ImageUrl = $"/images/{safeName}";
        await _db.SaveChangesAsync(ct);

        return Ok(new { poi.Id, poi.ImageUrl });
    }
}
