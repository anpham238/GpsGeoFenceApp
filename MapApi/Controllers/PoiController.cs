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

    // API: Lấy danh sách tất cả POI (Cho App Mobile gọi để hiển thị lên bản đồ)
    [HttpGet]
    public async Task<IActionResult> GetAllPois()
    {
        var pois = await _db.Pois.Where(p => p.IsActive).ToListAsync();
        return Ok(pois);
    }
    public async Task<IActionResult> CreateOrUpdatePoi([FromBody] PoiCreateDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Id) || string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Id và Name (Tiếng Việt) là bắt buộc.");

        // Tạo object POI chuẩn
        var poi = new Poi
        {
            Id = dto.Id,
            Latitude = dto.Lat,
            Longitude = dto.Lng,
            RadiusMeters = dto.Radius > 0 ? dto.Radius : 100, // Mặc định 100m
            CooldownSeconds = 30,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Gọi Service: Vừa lưu POI, vừa tự động dịch Tên/Mô tả/Thuyết minh sang các ngôn ngữ khác
        await _poiService.AddOrUpdatePoiWithAutoTranslationAsync(
            poi,
            viName: dto.Name,
            viDesc: dto.Description,
            viNarration: dto.NarrationText
        );

        return Ok(new { message = "Lưu thành công và đã tự động dịch đa ngôn ngữ!", poiId = poi.Id });
    }
}

// DTO (Data Transfer Object) để nhận dữ liệu từ Admin (Postman / Web Admin)
public record PoiCreateDto(
    string Id,
    string Name, // Tên Tiếng Việt
    string? Description, // Mô tả Tiếng Việt
    string? NarrationText, // Thuyết minh Tiếng Việt
    double Lat,
    double Lng,
    int Radius = 100
);