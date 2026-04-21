using MapApi.Data;
using MapApi.Models;
using MapApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Controllers;

[ApiController]
[Route("api/v1/pois")]
public class PoiController : ControllerBase
{
    private readonly AppDb _db;
    private readonly PoiManagementService _poiService;
    public PoiController(AppDb db, PoiManagementService poiService)
    {
        _db = db;
        _poiService = poiService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrUpdatePoi([FromBody] PoiCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name (Tiếng Việt) là bắt buộc.");

        // Tạo object POI chuẩn
        var poi = new Poi
        {
            Latitude = dto.Lat,
            Longitude = dto.Lng,
            Name = dto.Name,
            Description = dto.Description,
            RadiusMeters = dto.Radius > 0 ? dto.Radius : 100,
            CooldownSeconds = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Gọi Service: Vừa lưu POI, vừa tự động dịch Mô tả/Thuyết minh sang các ngôn ngữ khác
        await _poiService.AddOrUpdatePoiWithAutoTranslationAsync(
            poi,
            viNarration: dto.NarrationText,
            viDesc: dto.Description
        );

        return Ok(new { message = "Lưu thành công và đã tự động dịch đa ngôn ngữ!", poiId = poi.Id });
    }
    [HttpGet]
    public async Task<IActionResult> GetAllPois()
    {
        // Lấy danh sách POI đang Active và móc nối (JOIN) với PoiMedia và PoiLanguage
        var pois = await _db.Pois
            .Where(p => p.IsActive)
            .Select(p => new PoiDto // <--- SỬA CHỮ 'Poi' THÀNH 'PoiDto' Ở ĐÂY
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                RadiusMeters = p.RadiusMeters,
                CooldownSeconds = p.CooldownSeconds,
                IsActive = p.IsActive,
                UpdatedAt = p.UpdatedAt,

                // LẤY DỮ LIỆU TỪ BẢNG PoiMedia
                ImageUrl = _db.PoiMedia.Where(m => m.IdPoi == p.Id).Select(m => m.Image).FirstOrDefault(),
                MapLink = _db.PoiMedia.Where(m => m.IdPoi == p.Id).Select(m => m.MapLink).FirstOrDefault(),
                AudioUrl = null,

                // Lấy Text thuyết minh tiếng Việt từ bảng PoiLanguage (nếu có)
                NarrationText = _db.PoiLanguages.Where(l => l.IdPoi == p.Id && l.LanguageTag == "vi-VN").Select(l => l.TextToSpeech).FirstOrDefault(),
                Language = "vi-VN"
            })
            .ToListAsync();

        return Ok(pois);
    }
}

// DTO để nhận dữ liệu từ Admin (Postman / Web Admin)
public record PoiCreateDto(
    string Name,              // Tên Tiếng Việt (bắt buộc)
    string? Description,      // Mô tả Tiếng Việt
    string? NarrationText,    // Thuyết minh Tiếng Việt
    double Lat,
    double Lng,
    int Radius = 100
);
// DTO để trả dữ liệu danh sách POI về cho App Mobile
public record PoiDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int RadiusMeters { get; set; }
    public int CooldownSeconds { get; set; }
    public bool IsActive { get; set; }
    public DateTime UpdatedAt { get; set; }
    // Các trường lấy từ các bảng phụ (Media & Language)
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string? AudioUrl { get; set; }
    public string? NarrationText { get; set; }
    public string? Language { get; set; }
}
