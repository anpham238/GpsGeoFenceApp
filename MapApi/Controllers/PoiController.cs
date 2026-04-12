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
            Latitude        = dto.Lat,
            Longitude       = dto.Lng,
            Name            = dto.Name,
            Description     = dto.Description,
            RadiusMeters    = dto.Radius > 0 ? dto.Radius : 100,
            CooldownSeconds = 30,
            IsActive        = true,
            CreatedAt       = DateTime.UtcNow,
            UpdatedAt       = DateTime.UtcNow
        };

        // Gọi Service: Vừa lưu POI, vừa tự động dịch Mô tả/Thuyết minh sang các ngôn ngữ khác
        await _poiService.AddOrUpdatePoiWithAutoTranslationAsync(
            poi,
            viNarration: dto.NarrationText,
            viDesc:      dto.Description
        );

        return Ok(new { message = "Lưu thành công và đã tự động dịch đa ngôn ngữ!", poiId = poi.Id });
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
