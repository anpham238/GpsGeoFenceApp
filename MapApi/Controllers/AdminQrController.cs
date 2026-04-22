using MapApi.Data;
using MapApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace MapApi.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/admin/qr")]
public class AdminQrController(AppDb db) : ControllerBase
{
    [HttpPost("narration")]
    public async Task<IActionResult> CreateNarrationQr([FromBody] CreateNarrationQrRequest req)
    {
        if (req.PoiId <= 0)
            return BadRequest(new { message = "PoiId must be greater than 0." });

        var poiExists = await db.Pois.AsNoTracking().AnyAsync(x => x.Id == req.PoiId && x.IsActive);
        if (!poiExists)
            return NotFound(new { message = "POI not found." });

        var languageTag = string.IsNullOrWhiteSpace(req.LanguageTag) ? "vi-VN" : req.LanguageTag.Trim();
        var languageExists = await db.SupportedLanguages.AsNoTracking()
            .AnyAsync(x => x.LanguageTag == languageTag && x.IsActive);
        if (!languageExists)
            return BadRequest(new { message = "Language is not supported or inactive." });

        var ticketCode = "QR-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var ticket = new PoiTicket
        {
            TicketCode = ticketCode,
            IdPoi = req.PoiId,
            LanguageTag = languageTag,
            MaxUses = req.MaxUses is > 0 ? req.MaxUses.Value : 5,
            CurrentUses = 0,
            CreatedAt = DateTime.UtcNow
        };

        db.PoiTickets.Add(ticket);
        await db.SaveChangesAsync();

        var payload = new
        {
            type = "narration",
            ticketCode,
            poiId = ticket.IdPoi,
            languageTag = ticket.LanguageTag
        };

        var qrPng = GenerateQrPng(System.Text.Json.JsonSerializer.Serialize(payload));

        return Ok(new
        {
            type = "narration",
            ticketCode,
            qrImageBase64 = Convert.ToBase64String(qrPng)
        });
    }

    [HttpPost("distribution")]
    public async Task<IActionResult> CreateDistributionQr([FromBody] CreateDistributionQrRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.LocationName))
            return BadRequest(new { message = "LocationName is required." });

        var source = new AppDownloadSource
        {
            LocationName = req.LocationName.Trim(),
            CampaignCode = string.IsNullOrWhiteSpace(req.CampaignCode) ? null : req.CampaignCode.Trim()
        };

        db.AppDownloadSources.Add(source);
        await db.SaveChangesAsync();

        var link = $"{Request.Scheme}://{Request.Host}/api/v1/download/{source.SourceId}";
        var qrPng = GenerateQrPng(link);

        return Ok(new
        {
            type = "distribution",
            sourceId = source.SourceId,
            smartLink = link,
            qrImageBase64 = Convert.ToBase64String(qrPng)
        });
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetQrHistory()
    {
        var narration = await db.PoiTickets.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new
            {
                Type = "narration",
                CreatedAt = x.CreatedAt,
                Code = x.TicketCode,
                PoiId = x.IdPoi,
                LanguageTag = x.LanguageTag,
                MaxUses = x.MaxUses,
                CurrentUses = x.CurrentUses
            })
            .ToListAsync();

        var distributions = await db.AppDownloadSources.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .Select(x => new
            {
                Type = "distribution",
                CreatedAt = x.CreatedAt,
                SourceId = x.SourceId,
                x.LocationName,
                x.CampaignCode,
                SmartLink = $"{Request.Scheme}://{Request.Host}/api/v1/download/{x.SourceId}"
            })
            .ToListAsync();

        return Ok(new
        {
            Narration = narration,
            Distribution = distributions
        });
    }

    private static byte[] GenerateQrPng(string payload)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(20);
    }
}

public sealed class CreateNarrationQrRequest
{
    public int PoiId { get; set; }
    public string? LanguageTag { get; set; }
    public int? MaxUses { get; set; }
}

public sealed class CreateDistributionQrRequest
{
    public string LocationName { get; set; } = string.Empty;
    public string? CampaignCode { get; set; }
}
