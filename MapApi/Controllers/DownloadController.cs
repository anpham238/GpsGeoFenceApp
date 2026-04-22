using MapApi.Data;
using MapApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DownloadController(AppDb db) : ControllerBase
{
    // TODO: Cập nhật link tải app thực tế
    private const string AndroidStoreUrl = "market://details?id=com.your.gpsapp";
    private const string AppleStoreUrl = "itms-apps://itunes.apple.com/app/id123456789";
    private const string FallbackUrl = "https://yourdomain.com/landing";

    [HttpGet("{sourceId}")]
    public async Task<IActionResult> RedirectAndTrack(int sourceId)
    {
        var userAgent = Request.Headers.UserAgent.ToString().ToLower();
        string platform = "Web";
        string redirectUrl = FallbackUrl;

        if (userAgent.Contains("android"))
        {
            platform = "Android";
            redirectUrl = AndroidStoreUrl;
        }
        else if (userAgent.Contains("iphone") || userAgent.Contains("ipad"))
        {
            platform = "iOS";
            redirectUrl = AppleStoreUrl;
        }

        // Kiểm tra xem mã QR có tồn tại trong hệ thống không
        var sourceExists = await db.AppDownloadSources.AnyAsync(x => x.SourceId == sourceId);
        if (sourceExists)
        {
            // Chỉ ghi log nếu sourceId hợp lệ để tránh lỗi Foreign Key
            var scanLog = new AnalyticsAppDownloadScan
            {
                SourceId = sourceId,
                Platform = platform
            };
            
            db.AnalyticsAppDownloadScans.Add(scanLog);
            await db.SaveChangesAsync();
        }

        return Redirect(redirectUrl);
    }

    [HttpGet("/api/v1/admin/analytics/downloads")]
    public async Task<IActionResult> GetAnalytics()
    {
        var scans = await db.AnalyticsAppDownloadScans
            .GroupBy(x => new { x.SourceId, x.Platform })
            .Select(g => new 
            {
                SourceId = g.Key.SourceId,
                Platform = g.Key.Platform,
                Count = g.Count()
            })
            .ToListAsync();

        var sourcesDict = await db.AppDownloadSources.ToDictionaryAsync(x => x.SourceId);
        
        var result = scans.Select(s => new 
        {
            SourceId = s.SourceId,
            LocationName = sourcesDict.TryGetValue(s.SourceId, out var src) ? src.LocationName : "Unknown",
            CampaignCode = sourcesDict.TryGetValue(s.SourceId, out var src2) ? src2.CampaignCode : "",
            Platform = s.Platform,
            Count = s.Count
        }).OrderByDescending(x => x.Count);

        return Ok(result);
    }
}
