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

        var poi = new Poi
        {
            Latitude = dto.Lat,
            Longitude = dto.Lng,
            Name = dto.Name,
            Description = dto.Description,
            RadiusMeters = dto.Radius > 0 ? dto.Radius : 100,
            CooldownSeconds = 30,
            PriorityLevel = dto.PriorityLevel,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

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
        var pois = await _db.Pois.AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync();

        if (pois.Count == 0) return Ok(pois);

        var poiIds = pois.Select(p => p.Id).ToList();

        // 2 queries thay vì N×2 correlated subqueries
        var mediaMap = await _db.PoiMedia.AsNoTracking()
            .Where(m => poiIds.Contains(m.IdPoi))
            .ToDictionaryAsync(m => m.IdPoi);

        var langMap = await _db.PoiLanguages.AsNoTracking()
            .Where(l => poiIds.Contains(l.IdPoi) && l.LanguageTag == "vi-VN")
            .ToDictionaryAsync(l => l.IdPoi);

        var dtos = pois.Select(p =>
        {
            mediaMap.TryGetValue(p.Id, out var media);
            langMap.TryGetValue(p.Id, out var lang);
            return new PoiDto
            {
                Id              = p.Id,
                Name            = p.Name,
                Description     = p.Description,
                Latitude        = p.Latitude,
                Longitude       = p.Longitude,
                RadiusMeters    = p.RadiusMeters,
                CooldownSeconds = p.CooldownSeconds,
                IsActive        = p.IsActive,
                PriorityLevel   = p.PriorityLevel,
                UpdatedAt       = p.UpdatedAt,
                ImageUrl        = media?.Image,
                MapLink         = media?.MapLink,
                AudioUrl        = null,
                NarrationText   = lang?.TextToSpeech,
                Language        = "vi-VN"
            };
        }).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPoiById(int id)
    {
        var p = await _db.Pois.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();

        // 2 queries thay vì 4 queries riêng biệt
        var media = await _db.PoiMedia.AsNoTracking().FirstOrDefaultAsync(m => m.IdPoi == id);
        var lang  = await _db.PoiLanguages.AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdPoi == id && l.LanguageTag == "vi-VN");

        return Ok(new PoiDto
        {
            Id              = p.Id,
            Name            = p.Name,
            Description     = p.Description,
            Latitude        = p.Latitude,
            Longitude       = p.Longitude,
            RadiusMeters    = p.RadiusMeters,
            CooldownSeconds = p.CooldownSeconds,
            IsActive        = p.IsActive,
            PriorityLevel   = p.PriorityLevel,
            UpdatedAt       = p.UpdatedAt,
            ImageUrl        = media?.Image,
            MapLink         = media?.MapLink,
            AudioUrl        = lang?.ProAudioUrl,
            NarrationText   = lang?.TextToSpeech,
            Language        = "vi-VN"
        });
    }
}

public record PoiCreateDto(
    string Name,
    string? Description,
    string? NarrationText,
    double Lat,
    double Lng,
    int Radius = 100,
    int PriorityLevel = 0
);

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
    public int PriorityLevel { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? ImageUrl { get; set; }
    public string? MapLink { get; set; }
    public string? AudioUrl { get; set; }
    public string? NarrationText { get; set; }
    public string? Language { get; set; }
}
