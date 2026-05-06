using System.Security.Claims;
using MapApi.Contracts;
using MapApi.Contracts.Realtime;
using MapApi.Data;
using MapApi.Models;
using MapApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);
// 1) Connection string
var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Server=.\\SQLEXPRESS;Database=GpsApi;Trusted_Connection=True;"
          + "Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
// 2) JWT config (lấy từ appsettings.json)
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? "CHANGE_THIS_SECRET_KEY_MIN_32_CHARS_PLEASE";

// 3) EF Core
builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseSqlServer(cs, sql =>
    {
        sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        sql.CommandTimeout(120);
    }));

// 4) JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

// 5) Swagger + CORS + Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));
builder.Services.AddHttpClient<TranslatorClient>();
builder.Services.AddScoped<PoiManagementService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NarrationTextService>();
builder.Services.AddHostedService<TranslationBackgroundService>();
builder.Services.AddScoped<IHistoryService, HistoryService>();
builder.Services.AddSingleton<IDevicePresenceService, DevicePresenceService>();
builder.Services.AddSignalR();
var app = builder.Build();
app.UseCors();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }));

// ============================================================
// GET /api/v1/app/download?platform=android|ios
// Phục vụ APK trực tiếp hoặc redirect về store
// ============================================================
app.MapGet("/api/v1/app/download", (string? platform, IWebHostEnvironment env) =>
{
    var isIos = string.Equals(platform, "ios", StringComparison.OrdinalIgnoreCase);
    if (isIos)
        return Results.Redirect("https://apps.apple.com/app/id000000000");

    // Android: phục vụ APK nếu có trong wwwroot/downloads/app.apk
    var apkPath = Path.Combine(env.WebRootPath, "downloads", "app.apk");
    if (System.IO.File.Exists(apkPath))
        return Results.File(apkPath, "application/vnd.android.package-archive", "SmartTourism.apk");

    // Chưa có APK → báo lỗi rõ ràng
    return Results.Problem(
        title: "APK chưa sẵn sàng",
        detail: "Vui lòng đặt file APK vào thư mục: MapApi/wwwroot/downloads/app.apk",
        statusCode: 404);
});

// ============================================================
// GET /api/v1/admin/server-info — Trả về LAN IP để admin tạo QR đúng URL
// Luôn dùng HTTP (không HTTPS) vì phone không trust self-signed cert
// ============================================================
app.MapGet("/api/v1/admin/server-info", (IConfiguration config) =>
{
    var host = System.Net.Dns.GetHostName();
    var lanIps = System.Net.Dns.GetHostAddresses(host)
        .Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        .Select(a => a.ToString())
        .Where(a => a.StartsWith("192.168.") || a.StartsWith("10.") || a.StartsWith("172."))
        .ToList();

    // Luôn lấy cổng HTTP (5150) vì HTTPS self-signed cert không work trên điện thoại
    var allUrls = (config["ASPNETCORE_URLS"] ?? "http://0.0.0.0:5150").Split(';');
    var httpUrl = allUrls.FirstOrDefault(u => u.StartsWith("http://")) ?? "http://0.0.0.0:5150";
    var httpPort = new Uri(httpUrl.Replace("0.0.0.0", "localhost").Replace("+", "localhost")).Port;

    var suggestedUrls = lanIps.Select(ip => $"http://{ip}:{httpPort}").ToList();

    return Results.Ok(new { hostname = host, lanIps, httpPort, suggestedUrls });
});
// ============================================================
// GET /api/v1/admin/seed/status — Kiểm tra bảng PoiLanguage đã có bao nhiêu ngôn ngữ
// ============================================================
app.MapGet("/api/v1/admin/seed/status", async (AppDb db) =>
{
    var langs = await db.PoiLanguages
        .GroupBy(x => x.LanguageTag)
        .Select(g => new { Language = g.Key, Count = g.Count() })
        .OrderBy(x => x.Language)
        .ToListAsync();

    var pois = await db.Pois.CountAsync();

    return Results.Ok(new
    {
        TotalPois       = pois,
        TotalTranslations = langs.Sum(x => x.Count),
        ByLanguage      = langs
    });
})
.WithName("SeedStatus")
.WithSummary("Kiểm tra số bản dịch trong bảng PoiLanguage");

// ============================================================
// POST /api/v1/admin/translate-all — Dịch tất cả POI hiện có sang 4 ngôn ngữ
// Query: ?overwrite=false  (true = ghi đè bản dịch cũ)
// ============================================================
app.MapPost("/api/v1/admin/translate-all", async (
    bool? overwrite, AppDb db, PoiManagementService svc) =>
{
    var force = overwrite ?? false;
    var pois  = await db.Pois.AsNoTracking().Where(p => p.IsActive).ToListAsync();
    var log   = new List<string>();
    int done  = 0, skipped = 0;

    foreach (var poi in pois)
    {
        // Kiểm tra đã dịch đủ chưa (nếu không overwrite): vi-VN + 5 ngôn ngữ = 6
        if (!force)
        {
            var existCount = await db.PoiLanguages
                .CountAsync(x => x.IdPoi == poi.Id);
            if (existCount >= 6)
            {
                log.Add($"[SKIP] {poi.Id} — đã có {existCount} ngôn ngữ");
                skipped++;
                continue;
            }
        }

        // Lấy bản nguồn vi-VN (ưu tiên từ PoiLanguage.TextToSpeech, fallback từ Pois.Description)
        var viRow = await db.PoiLanguages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.IdPoi == poi.Id && x.LanguageTag == "vi-VN");

        var viNarration = viRow?.TextToSpeech;  // đã là combined (NarTTS + Desc)
        var viDesc      = poi.Description;       // Description gốc từ bảng Pois

        var progress = new Progress<string>(msg => log.Add(msg));
        await svc.AddOrUpdatePoiWithAutoTranslationAsync(poi, viNarration, viDesc, progress);
        done++;
    }

    return Results.Ok(new { Translated = done, Skipped = skipped, Log = log });
})
.WithName("TranslateAll")
.WithSummary("Dịch tất cả POI đang có sang 5 ngôn ngữ (en-US, zh-Hans, ja-JP, ko-KR, de-DE)");
// ============================================================
// GET /api/v1/pois/{id}
// ============================================================
app.MapGet("/api/v1/pois/{id}", async (int id, AppDb db) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });

    var media = await db.PoiMedia.AsNoTracking()
        .FirstOrDefaultAsync(m => m.IdPoi == id);

    return Results.Ok(new
    {
        poi.Longitude,
        poi.RadiusMeters,
        poi.CooldownSeconds,
        poi.IsActive,
        poi.UpdatedAt,
        NearRadiusMeters = poi.RadiusMeters * 2,
        DebounceSeconds = 3,
        ImageUrl = media?.Image,
        MapLink  = media?.MapLink,
        AudioUrl = (string?)null,
        Language = "vi-VN",
        NarrationText = (string?)null
    });
});

// ============================================================
// GET /api/v1/pois/{id}/narration?lang=...&eventType=enter|near|tap
// ============================================================
app.MapGet("/api/v1/pois/{id}/narration", async (
    int id, string? lang, string? eventType,
    HttpContext ctx, AppDb db, NarrationTextService narSvc, CancellationToken ct) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });

    var toLang = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang.Trim();
    var evt    = NarrationTextService.ParseEventType(eventType);

    // Gate ngôn ngữ premium
    var langInfo = await db.SupportedLanguages.AsNoTracking()
        .FirstOrDefaultAsync(l => l.LanguageTag == toLang && l.IsActive, ct);
    if (langInfo?.IsPremium == true)
    {
        var idClaimNar = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isPro = false;
        if (Guid.TryParse(idClaimNar, out var narUserId))
        {
            var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == narUserId && x.IsActive, ct);
            isPro = u?.PlanType == "PRO";
        }
        if (!isPro)
            return Results.Json(
                new { error = "Ngôn ngữ này nằm trong gói Premium. Vui lòng nâng cấp.", requirePro = true },
                statusCode: StatusCodes.Status403Forbidden);
    }

    var poiLang = await db.PoiLanguages.AsNoTracking()
        .FirstOrDefaultAsync(n => n.IdPoi == id && n.LanguageTag == toLang, ct);

    if (poiLang is null && toLang != "vi-VN")
        poiLang = await db.PoiLanguages.AsNoTracking()
            .FirstOrDefaultAsync(n => n.IdPoi == id && n.LanguageTag == "vi-VN", ct);

    var narText = narSvc.Build(poi.Name, poiLang?.TextToSpeech, toLang, evt);

    return Results.Ok(new
    {
        PoiId         = id,
        EventType     = evt,
        Language      = toLang,
        NarrationText = narText,
        Cached        = poiLang != null
    });
});
// ============================================================
// POST /api/v1/auth/register  (multipart/form-data)
// ============================================================
app.MapPost("/api/v1/auth/register", (HttpContext ctx, AuthService auth) => auth.RegisterAsync(ctx));

// ============================================================
// POST /api/v1/auth/login  — Smart Login (Username hoặc Email)
// ============================================================
app.MapPost("/api/v1/auth/login", (LoginRequest req, AuthService auth) => auth.LoginAsync(req));

// ============================================================
// GET /api/v1/auth/me  — Lấy profile từ JWT
// ============================================================
app.MapGet("/api/v1/auth/me", (HttpContext ctx, AuthService auth) =>
{
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Guid.TryParse(idClaim, out var userId)
        ? auth.GetProfileAsync(userId)
        : Task.FromResult(Results.Unauthorized());
}).RequireAuthorization();

// ============================================================
// PUT /api/v1/profile — Cập nhật thông tin cá nhân
// ============================================================
app.MapPut("/api/v1/profile", (HttpContext ctx, AuthService auth) =>
{
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Guid.TryParse(idClaim, out var userId)
        ? auth.UpdateProfileAsync(userId, ctx)
        : Task.FromResult(Results.Unauthorized());
}).RequireAuthorization();

// ============================================================
// PUT /api/v1/profile/change-password — Đổi mật khẩu
// ============================================================
app.MapPut("/api/v1/profile/change-password", (HttpContext ctx, ChangePasswordRequest req, AuthService auth) =>
{
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Guid.TryParse(idClaim, out var userId)
        ? auth.ChangePasswordAsync(userId, req)
        : Task.FromResult(Results.Unauthorized());
}).RequireAuthorization();

// ============================================================
// GET /api/v1/profile/history — Lịch sử tham quan POI của user
// ============================================================
app.MapGet("/api/v1/profile/history", async (HttpContext ctx, AppDb db) =>
{
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    var history = await db.HistoryPoi.AsNoTracking()
        .Where(h => h.IdUser == userId)
        .OrderByDescending(h => h.LastVisitedAt)
        .Take(50)
        .Select(h => new
        {
            h.Id, h.IdPoi, h.PoiName, h.Quantity,
            h.LastVisitedAt, h.TotalDurationSeconds
        })
        .ToListAsync();

    return Results.Ok(history);
}).RequireAuthorization();

// ============================================================
// GET /api/v1/profile/travel-history?sessionId=... — Nhật ký hành trình (PRO)
// ============================================================
app.MapGet("/api/v1/profile/travel-history", async (
    HttpContext ctx, string? sessionId, AppDb db) =>
{
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
    if (user is null) return Results.NotFound();

    if (user.PlanType != "PRO")
        return Results.Json(new { error = "Tính năng này chỉ dành cho Gói PRO", requirePro = true },
            statusCode: StatusCodes.Status403Forbidden);

    IQueryable<AnalyticsRoute> query = db.AnalyticsRoutes.AsNoTracking();

    if (Guid.TryParse(sessionId, out var sid))
        query = query.Where(r => r.SessionId == sid);
    else
        query = query.Where(r => false);

    var points = await query
        .OrderBy(r => r.RecordedAt)
        .Select(r => new { r.Latitude, r.Longitude, r.RecordedAt })
        .ToListAsync();

    return Results.Ok(points);
}).RequireAuthorization();

// ============================================================
// GET /api/v1/pois/{id}/directions?userLat=...&userLng=... — Chỉ đường tới POI (PRO only)
// Gọi OSRM để lấy tuyến đường thực tế và trả về mảng tọa độ Polyline
// ============================================================
app.MapGet("/api/v1/pois/{id}/directions", async (
    int id, double? userLat, double? userLng,
    HttpContext ctx, AppDb db, IHttpClientFactory httpFactory) =>
{
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
    if (user is null) return Results.NotFound();

    if (user.PlanType != "PRO")
        return Results.Json(new { error = "Chỉ đường chỉ dành cho Gói PRO", requirePro = true },
            statusCode: StatusCodes.Status403Forbidden);

    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });

    // Nếu không có tọa độ user → trả về thông tin POI để App tự xử lý
    if (userLat is null || userLng is null)
        return Results.Ok(new
        {
            PoiId = poi.Id, PoiName = poi.Name,
            Destination = new { Lat = poi.Latitude, Lng = poi.Longitude },
            RouteCoordinates = (object?)null,
            Message = "Cung cấp userLat & userLng để lấy tuyến đường OSRM"
        });

    // Gọi OSRM public API
    try
    {
        var client = httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);
        var osrmUrl = $"http://router.project-osrm.org/route/v1/driving/" +
                      $"{userLng!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{userLat!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)};" +
                      $"{poi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                      $"{poi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                      "?overview=full&geometries=geojson";

        var resp = await client.GetAsync(osrmUrl);
        if (resp.IsSuccessStatusCode)
        {
            var json = await resp.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var coords = doc.RootElement
                .GetProperty("routes")[0]
                .GetProperty("geometry")
                .GetProperty("coordinates");

            var route = new System.Collections.Generic.List<object>();
            foreach (var c in coords.EnumerateArray())
                route.Add(new { Lng = c[0].GetDouble(), Lat = c[1].GetDouble() });

            var distanceM = doc.RootElement.GetProperty("routes")[0].GetProperty("distance").GetDouble();
            var durationS = doc.RootElement.GetProperty("routes")[0].GetProperty("duration").GetDouble();

            return Results.Ok(new
            {
                PoiId = poi.Id, PoiName = poi.Name,
                Destination = new { Lat = poi.Latitude, Lng = poi.Longitude },
                DistanceMeters = distanceM,
                DurationSeconds = durationS,
                RouteCoordinates = route
            });
        }
    }
    catch { /* OSRM không khả dụng → fallback */ }

    // Fallback: trả về đường thẳng 2 điểm
    return Results.Ok(new
    {
        PoiId = poi.Id, PoiName = poi.Name,
        Destination = new { Lat = poi.Latitude, Lng = poi.Longitude },
        RouteCoordinates = new[]
        {
            new { Lng = userLng!.Value, Lat = userLat!.Value },
            new { Lng = poi.Longitude,  Lat = poi.Latitude  }
        },
        Message = "Đường thẳng (OSRM không khả dụng)"
    });
}).RequireAuthorization();

// ============================================================
// POST /api/v1/profile/upgrade — Nâng cấp lên PRO (demo: +30 ngày)
// ============================================================
app.MapPost("/api/v1/profile/upgrade", (HttpContext ctx, AuthService auth) =>
{
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Guid.TryParse(idClaim, out var userId)
        ? auth.UpgradeAsync(userId)
        : Task.FromResult(Results.Unauthorized());
}).RequireAuthorization();

// ============================================================
// POST /api/v1/profile/history — Ghi lịch sử ghé thăm từ user hiện tại
// ============================================================
app.MapPost("/api/v1/profile/history", async (
    HttpContext ctx, ProfileHistoryUpsertRequest req, IHistoryService historySvc) =>
{
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    if (req.PoiId <= 0) return Results.BadRequest(new { error = "PoiId là bắt buộc" });

    var result = await historySvc.UpsertVisitAsync(req.PoiId, userId, req.DurationSeconds);
    if (!result.Success)
        return Results.BadRequest(new { error = result.Error ?? "Không thể ghi lịch sử" });
    return Results.Ok(new { ok = true });
}).RequireAuthorization();

// ============================================================
// POST /api/v1/history — Ghi lịch sử ghé thăm (thay /api/v1/playback)
// ============================================================
app.MapPost("/api/v1/history", async (HistoryRequest req, IHistoryService historySvc) =>
{
    var result = await historySvc.UpsertVisitAsync(req.PoiId, req.UserId, req.DurationSeconds);
    if (!result.Success)
        return Results.BadRequest(new { error = result.Error ?? "Không thể ghi lịch sử" });
    return Results.Ok(new { ok = true });
});
// ============================================================
// GET /api/v1/admin/pois — Danh sách toàn bộ POI (kể cả inactive)
// ============================================================
app.MapGet("/api/v1/admin/pois", async (AppDb db) =>
{
    var pois = await db.Pois.AsNoTracking()
        .OrderByDescending(p => p.UpdatedAt)
        .Select(p => new
        {
            p.Id, p.Name, p.Description, p.Latitude, p.Longitude,
            p.RadiusMeters, p.CooldownSeconds, p.IsActive, p.PriorityLevel, p.CreatedAt, p.UpdatedAt
        })
        .ToListAsync();
    return Results.Ok(pois);
});

// ============================================================
// PUT /api/v1/admin/pois/{id} — Cập nhật POI (tên, mô tả, bán kính, toạ độ)
// ============================================================
app.MapPut("/api/v1/admin/pois/{id}", async (int id, PoiUpdateRequest req, AppDb db) =>
{
    var poi = await db.Pois.FirstOrDefaultAsync(p => p.Id == id);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });

    if (!string.IsNullOrWhiteSpace(req.Name))    poi.Name        = req.Name.Trim();
    if (req.Description is not null)             poi.Description = req.Description;
    if (req.Latitude.HasValue)                   poi.Latitude    = req.Latitude.Value;
    if (req.Longitude.HasValue)                  poi.Longitude   = req.Longitude.Value;
    if (req.RadiusMeters.HasValue)               poi.RadiusMeters = req.RadiusMeters.Value;
    if (req.CooldownSeconds.HasValue)            poi.CooldownSeconds = req.CooldownSeconds.Value;
    if (req.PriorityLevel.HasValue)              poi.PriorityLevel = req.PriorityLevel.Value;
    poi.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();

    // Cập nhật ImageUrl nếu có (URL ngoài hoặc nội bộ)
    if (!string.IsNullOrWhiteSpace(req.ImageUrl))
    {
        var media = await db.PoiMedia.FirstOrDefaultAsync(m => m.IdPoi == id);
        if (media is not null)
            media.Image = req.ImageUrl;
        else
            db.PoiMedia.Add(new MapApi.Models.PoiMedia { IdPoi = id, Image = req.ImageUrl });

        await db.SaveChangesAsync();
    }

    return Results.Ok(new { ok = true, poiId = poi.Id });
});

// ============================================================
// DELETE /api/v1/admin/pois/{id} — Soft-delete (IsActive = false)
// ============================================================
app.MapDelete("/api/v1/admin/pois/{id}", async (int id, AppDb db) =>
{
    var poi = await db.Pois.FirstOrDefaultAsync(p => p.Id == id);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });

    poi.IsActive  = false;
    poi.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

// ============================================================
// GET /api/v1/sync/version — Mobile kiểm tra version trước khi sync
// ============================================================
app.MapGet("/api/v1/sync/version", async (AppDb db) =>
{
    var latest = await db.Pois.AsNoTracking()
        .Where(p => p.IsActive)
        .MaxAsync(p => (DateTime?)p.UpdatedAt);
    var count = await db.Pois.CountAsync(p => p.IsActive);
    var version = latest?.ToString("O") ?? "0";
    return Results.Ok(new { version, count });
});

// ============================================================
// GET /api/v1/sync/tours — Danh sách tour kèm POI IDs
// ============================================================
app.MapGet("/api/v1/sync/tours", async (AppDb db) =>
{
    var tours = await db.Tours.AsNoTracking()
        .Where(t => t.IsActive)
        .Include(t => t.TourPois)
        .OrderBy(t => t.Id)
        .Select(t => new
        {
            t.Id,
            t.Name,
            t.Description,
            PoiIds = t.TourPois.OrderBy(tp => tp.SortOrder).Select(tp => tp.PoiId).ToList()
        })
        .ToListAsync();
    return Results.Ok(tours);
});

// ============================================================
// POST /api/v1/analytics/visit
// ============================================================
app.MapPost("/api/v1/analytics/visit", async (AnalyticsVisitRequest req, AppDb db) =>
{
    db.AnalyticsVisits.Add(new AnalyticsVisit
    {
        SessionId = req.SessionId,
        PoiId     = req.PoiId,
        Action    = req.Action,
        Timestamp = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

// POST /api/v1/analytics/route
app.MapPost("/api/v1/analytics/route", async (AnalyticsRouteRequest req, AppDb db) =>
{
    db.AnalyticsRoutes.Add(new AnalyticsRoute
    {
        SessionId  = req.SessionId,
        Latitude   = req.Latitude,
        Longitude  = req.Longitude,
        RecordedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

// POST /api/v1/analytics/listen-duration
app.MapPost("/api/v1/analytics/listen-duration", async (AnalyticsListenRequest req, AppDb db) =>
{
    db.AnalyticsListenDurations.Add(new AnalyticsListenDuration
    {
        SessionId       = req.SessionId,
        PoiId           = req.PoiId,
        DurationSeconds = req.DurationSeconds,
        RecordedAt      = DateTime.UtcNow
    });
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

// GET /api/v1/analytics/dashboard
app.MapGet("/api/v1/analytics/dashboard", async (AppDb db) =>
{
    var topPois = await db.AnalyticsVisits.AsNoTracking()
        .GroupBy(x => x.PoiId)
        .Select(g => new { PoiId = g.Key, TotalVisits = g.Count() })
        .OrderByDescending(x => x.TotalVisits)
        .Take(10)
        .ToListAsync();

    var avgDuration = await db.AnalyticsListenDurations.AsNoTracking()
        .GroupBy(x => x.PoiId)
        .Select(g => new { PoiId = g.Key, AvgSeconds = g.Average(x => x.DurationSeconds) })
        .ToListAsync();

    return Results.Ok(new { topPois, avgDuration });
});

// GET /api/v1/analytics/heatmap
app.MapGet("/api/v1/analytics/heatmap", async (AppDb db) =>
{
    var points = await db.AnalyticsRoutes.AsNoTracking()
        .OrderByDescending(x => x.RecordedAt)
        .Take(5000)
        .Select(x => new { x.Latitude, x.Longitude })
        .ToListAsync();
    return Results.Ok(points);
});
// ============================================================
// GET /api/v1/admin/stats — Dashboard: 4 số liệu tổng quan
// ============================================================
app.MapGet("/api/v1/admin/stats", async (AppDb db, IDevicePresenceService presence) =>
{
    var totalDevices  = await db.GuestDevices.CountAsync();
    var onlineDevices = presence.OnlineCount;
    var totalUsers    = await db.Users.CountAsync();
    var totalPro      = await db.Users.CountAsync(u => u.PlanType == "PRO");

    return Results.Ok(new
    {
        TotalDevices         = totalDevices,
        OnlineDevices        = onlineDevices,
        TotalRegisteredUsers = totalUsers,
        TotalProRevenue      = totalPro * 50000
    });
});

// ============================================================
// GET /api/v1/admin/users — Danh sách người dùng (tìm kiếm, phân trang)
// ============================================================
app.MapGet("/api/v1/admin/users", async (string? search, int? page, int? pageSize, AppDb db) =>
{
    var q = db.Users.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(search))
    {
        var s = search.Trim().ToLower();
        q = q.Where(u => u.Username.ToLower().Contains(s)
                      || u.Mail.ToLower().Contains(s)
                      || (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
    }
    var total = await q.CountAsync();
    var p  = Math.Max(1, page ?? 1);
    var ps = Math.Clamp(pageSize ?? 20, 1, 100);
    var items = await q
        .OrderByDescending(u => u.CreatedAt)
        .Skip((p - 1) * ps)
        .Take(ps)
        .Select(u => new
        {
            u.UserId, u.Username, u.Mail, u.PhoneNumber,
            u.AvatarUrl, u.IsActive, u.CreatedAt,
            u.PlanType, u.ProExpiryDate
        })
        .ToListAsync();
    return Results.Ok(new { total, page = p, pageSize = ps, items });
});

// ============================================================
// PUT /api/v1/admin/users/{id}/lock — Khóa / Mở khóa tài khoản
// ============================================================
app.MapPut("/api/v1/admin/users/{id:guid}/lock", async (Guid id, AppDb db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == id);
    if (user is null) return Results.NotFound();
    user.IsActive = !user.IsActive;
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true, isActive = user.IsActive });
});

// ============================================================
// PUT /api/v1/admin/users/{id} — Sửa thông tin người dùng
// ============================================================
app.MapPut("/api/v1/admin/users/{id:guid}", async (Guid id, AdminUserUpdateRequest req, AppDb db) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == id);
    if (user is null) return Results.NotFound();
    if (req.PhoneNumber is not null) user.PhoneNumber = req.PhoneNumber;
    if (req.PlanType is not null)
        user.PlanType = req.PlanType == "PRO" || req.PlanType == "FREE" ? req.PlanType : user.PlanType;
    if (req.ProExpiryDate.HasValue) user.ProExpiryDate = req.ProExpiryDate;
    await db.SaveChangesAsync();
    return Results.Ok(new { ok = true });
});

// ============================================================
// GET /api/v1/admin/devices — Danh sách thiết bị + trạng thái Online/Offline
// ============================================================
app.MapGet("/api/v1/admin/devices", async (AppDb db, IDevicePresenceService presence) =>
{
    var devices = await db.GuestDevices.AsNoTracking()
        .OrderByDescending(d => d.LastActiveAt)
        .Select(d => new
        {
            d.DeviceId, d.Platform, d.AppVersion,
            d.LastLatitude, d.LastLongitude,
            d.FirstSeenAt, d.LastActiveAt,
            IsOnline = presence.IsOnline(d.DeviceId)
        })
        .ToListAsync();
    return Results.Ok(devices);
});

// ============================================================
// GET /api/v1/admin/devices/realtime-snapshot — Contract snapshot cho CMS
// ============================================================
app.MapGet("/api/v1/admin/devices/realtime-snapshot", async (
    AppDb db, IDevicePresenceService presence, CancellationToken ct) =>
{
    var onlineCount = presence.OnlineCount;
    var devices = await db.GuestDevices.AsNoTracking()
        .OrderByDescending(x => x.LastActiveAt)
        .Select(x => new DevicePresenceDto
        {
            DeviceId = x.DeviceId,
            IsOnline = presence.IsOnline(x.DeviceId),
            OnlineCount = onlineCount,
            LastActiveAt = x.LastActiveAt,
            FirstSeenAt = x.FirstSeenAt,
            Platform = x.Platform,
            AppVersion = x.AppVersion,
            LastLatitude = x.LastLatitude,
            LastLongitude = x.LastLongitude
        })
        .ToListAsync(ct);

    return Results.Ok(new DevicePresenceSnapshotEnvelope
    {
        EmittedAt = DateTime.UtcNow,
        OnlineCount = onlineCount,
        Devices = devices
    });
});

// ============================================================
// POST /api/v1/usage/check — Kiểm tra & tăng lượt dùng Freemium
// Body: { entityId, actionType }
// Trả về 200 OK (hợp lệ) hoặc 402 Payment Required (hết lượt)
// PRO users bypass hoàn toàn
// ============================================================
app.MapPost("/api/v1/usage/check", async (UsageCheckRequest req, HttpContext ctx, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.EntityId) || string.IsNullOrWhiteSpace(req.ActionType))
        return Results.BadRequest(new { error = "EntityId và ActionType là bắt buộc" });

    var actionType = req.ActionType.ToUpperInvariant();
    if (actionType != "QR_SCAN" && actionType != "POI_LISTEN")
        return Results.BadRequest(new { error = "ActionType phải là QR_SCAN hoặc POI_LISTEN" });

    // Kiểm tra nếu là user PRO → bypass
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (Guid.TryParse(idClaim, out var userId))
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive);
        if (u?.PlanType == "PRO")
            return Results.Ok(new { allowed = true, bypassed = true, plan = "PRO" });
    }

    var maxLimit = 5;
    var now = DateTime.UtcNow;

    var tracking = await db.DailyUsageTrackings
        .FirstOrDefaultAsync(x => x.EntityId == req.EntityId && x.ActionType == actionType);

    if (tracking is null)
    {
        db.DailyUsageTrackings.Add(new MapApi.Models.DailyUsageTracking
        {
            EntityId = req.EntityId,
            ActionType = actionType,
            UsedCount = 1,
            LastResetAt = now
        });
        await db.SaveChangesAsync();
        return Results.Ok(new { allowed = true, used = 1, limit = maxLimit, resetIn = "24h" });
    }

    // Reset sau 24 giờ
    if ((now - tracking.LastResetAt).TotalHours >= 24)
    {
        tracking.UsedCount = 1;
        tracking.LastResetAt = now;
        await db.SaveChangesAsync();
        return Results.Ok(new { allowed = true, used = 1, limit = maxLimit, resetIn = "24h" });
    }

    if (tracking.UsedCount < maxLimit)
    {
        tracking.UsedCount++;
        await db.SaveChangesAsync();
        var resetInHours = 24 - (now - tracking.LastResetAt).TotalHours;
        return Results.Ok(new { allowed = true, used = tracking.UsedCount, limit = maxLimit, resetInHours });
    }

    // Hết lượt → 402
    var timeLeft = 24 - (now - tracking.LastResetAt).TotalHours;
    return Results.Json(
        new { allowed = false, used = tracking.UsedCount, limit = maxLimit, resetInHours = timeLeft,
              message = $"Hết lượt. Làm mới sau {timeLeft:F1} giờ nữa." },
        statusCode: 402);
});

// ============================================================
// GET /api/v1/usage/status — Chỉ xem số lượt còn lại (không tiêu thụ)
// ============================================================
app.MapGet("/api/v1/usage/status", async (string entityId, string actionType, HttpContext ctx, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(entityId) || string.IsNullOrWhiteSpace(actionType))
        return Results.BadRequest(new { error = "EntityId và ActionType là bắt buộc" });

    actionType = actionType.ToUpperInvariant();
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (Guid.TryParse(idClaim, out var userId))
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive);
        if (u?.PlanType == "PRO")
            return Results.Ok(new { allowed = true, bypassed = true, plan = "PRO", used = 0, limit = 9999, resetInHours = 0 });
    }

    var maxLimit = 5;
    var now = DateTime.UtcNow;
    var tracking = await db.DailyUsageTrackings.AsNoTracking()
        .FirstOrDefaultAsync(x => x.EntityId == entityId && x.ActionType == actionType);

    if (tracking is null)
        return Results.Ok(new { allowed = true, used = 0, limit = maxLimit, resetInHours = 0 });

    var elapsed = (now - tracking.LastResetAt).TotalHours;
    if (elapsed >= 24)
        return Results.Ok(new { allowed = true, used = 0, limit = maxLimit, resetInHours = 0 });

    var timeLeft = 24 - elapsed;
    if (tracking.UsedCount < maxLimit)
        return Results.Ok(new { allowed = true, used = tracking.UsedCount, limit = maxLimit, resetInHours = timeLeft });

    return Results.Ok(new { allowed = false, used = tracking.UsedCount, limit = maxLimit, resetInHours = timeLeft });
});

// ============================================================
// GET /api/v1/pois/{id}/content?lang={lang}
// FREE: trả về TextToSpeech. PRO: trả về ProAudioUrl + ProPodcastScript
// Gate ngôn ngữ Premium: FREE dùng ngôn ngữ IsPremium=1 → 403
// ============================================================
app.MapGet("/api/v1/pois/{id}/content", async (
    int id, string? lang, HttpContext ctx, AppDb db, CancellationToken ct) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id && p.IsActive, ct);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });

    var toLang = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang.Trim();

    // Kiểm tra ngôn ngữ có phải premium không
    var langInfo = await db.SupportedLanguages.AsNoTracking()
        .FirstOrDefaultAsync(l => l.LanguageTag == toLang && l.IsActive, ct);

    // Xác định plan của người dùng
    var isPro = false;
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (Guid.TryParse(idClaim, out var userId))
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId && x.IsActive, ct);
        isPro = u?.PlanType == "PRO";
    }

    // Gate ngôn ngữ premium
    if (langInfo?.IsPremium == true && !isPro)
        return Results.Json(
            new { error = "Ngôn ngữ này nằm trong gói Premium. Vui lòng nâng cấp để tiếp tục.", requirePro = true },
            statusCode: StatusCodes.Status403Forbidden);

    var poiLang = await db.PoiLanguages.AsNoTracking()
        .FirstOrDefaultAsync(l => l.IdPoi == id && l.LanguageTag == toLang, ct);

    // Fallback vi-VN
    if (poiLang is null && toLang != "vi-VN")
        poiLang = await db.PoiLanguages.AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdPoi == id && l.LanguageTag == "vi-VN", ct);

    if (isPro && !string.IsNullOrWhiteSpace(poiLang?.ProAudioUrl))
    {
        return Results.Ok(new
        {
            PoiId = id, Language = toLang, Plan = "PRO",
            ContentType = "studio_audio",
            ProAudioUrl = poiLang.ProAudioUrl,
            ProPodcastScript = poiLang.ProPodcastScript,
            TextToSpeech = (string?)null
        });
    }

    return Results.Ok(new
    {
        PoiId = id, Language = toLang, Plan = isPro ? "PRO" : "FREE",
        ContentType = "tts",
        ProAudioUrl = (string?)null,
        ProPodcastScript = (string?)null,
        TextToSpeech = poiLang?.TextToSpeech ?? ""
    });
});

// ============================================================
// GET /api/v1/languages — Danh sách ngôn ngữ (Public)
// Trả về cờ isPremium để App vẽ icon 🔒
// ============================================================
app.MapGet("/api/v1/languages", async (AppDb db, CancellationToken ct) =>
{
    var langs = await db.SupportedLanguages.AsNoTracking()
        .Where(l => l.IsActive)
        .OrderBy(l => l.IsPremium).ThenBy(l => l.LanguageTag)
        .Select(l => new { l.LanguageTag, l.LanguageName, l.IsPremium })
        .ToListAsync(ct);
    return Results.Ok(langs);
});

// ============================================================
// GET /api/v1/tours/{id}/offline-pack?lang={lang} — Gói Offline (PRO only)
// Trả về toàn bộ data Tour: POIs + tọa độ + text + URLs ảnh + audio
// ============================================================
app.MapGet("/api/v1/tours/{id}/offline-pack", async (
    int id, string? lang, HttpContext ctx, AppDb db, CancellationToken ct) =>
{
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive, ct);
    if (user is null) return Results.NotFound();

    if (user.PlanType != "PRO")
        return Results.Json(
            new { error = "Tính năng tải Tour ngoại tuyến chỉ dành cho Gói PRO", requirePro = true },
            statusCode: StatusCodes.Status403Forbidden);

    var tour = await db.Tours.AsNoTracking()
        .Include(t => t.TourPois)
        .FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct);
    if (tour is null) return Results.NotFound(new { error = "Tour not found" });

    var toLang = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang.Trim();
    var poiIds = tour.TourPois.OrderBy(tp => tp.SortOrder).Select(tp => tp.PoiId).ToList();

    var pois = await db.Pois.AsNoTracking()
        .Where(p => poiIds.Contains(p.Id) && p.IsActive)
        .ToListAsync(ct);

    var images = await db.PoiImages.AsNoTracking()
        .Where(img => poiIds.Contains(img.IdPoi))
        .ToListAsync(ct);

    var languages = await db.PoiLanguages.AsNoTracking()
        .Where(l => poiIds.Contains(l.IdPoi) && l.LanguageTag == toLang)
        .ToListAsync(ct);

    var pack = poiIds.Select(pid =>
    {
        var p = pois.FirstOrDefault(x => x.Id == pid);
        if (p is null) return null;
        var l = languages.FirstOrDefault(x => x.IdPoi == pid);
        var imgs = images.Where(x => x.IdPoi == pid).OrderBy(x => x.SortOrder)
                         .Select(x => x.ImageUrl).ToList();
        return new
        {
            p.Id, p.Name, p.Description, p.Latitude, p.Longitude,
            p.RadiusMeters, p.CooldownSeconds,
            Language = toLang,
            TextToSpeech = l?.TextToSpeech,
            ProAudioUrl = l?.ProAudioUrl,
            ProPodcastScript = l?.ProPodcastScript,
            ImageUrls = imgs
        };
    }).Where(x => x is not null).ToList();

    return Results.Ok(new
    {
        TourId = tour.Id,
        TourName = tour.Name,
        Language = toLang,
        GeneratedAt = DateTime.UtcNow,
        PoiCount = pack.Count,
        Pois = pack
    });
}).RequireAuthorization();

// ============================================================
// GET /api/v1/pois/{id}/images — Danh sách ảnh của POI
// Gộp cả PoiImages (nhiều ảnh) và PoiMedia.Image (ảnh chính)
// Hỗ trợ cả URL ngoài (http/https) và path nội bộ
// ============================================================
app.MapGet("/api/v1/pois/{id:int}/images", async (int id, AppDb db, CancellationToken ct) =>
{
    var images = new List<object>();

    // Ảnh từ bảng PoiImages (nhiều ảnh, có SortOrder)
    var poiImages = await db.PoiImages.AsNoTracking()
        .Where(img => img.IdPoi == id)
        .OrderBy(img => img.SortOrder)
        .Select(img => new { img.Id, img.ImageUrl, img.SortOrder })
        .ToListAsync(ct);

    foreach (var img in poiImages)
        images.Add(new { img.Id, img.ImageUrl, img.SortOrder, Source = "gallery" });

    // Ảnh chính từ PoiMedia.Image (nếu chưa có trong gallery)
    var media = await db.PoiMedia.AsNoTracking()
        .FirstOrDefaultAsync(m => m.IdPoi == id, ct);

    if (media is not null && !string.IsNullOrWhiteSpace(media.Image))
    {
        var alreadyIncluded = poiImages.Any(img =>
            img.ImageUrl.Equals(media.Image, StringComparison.OrdinalIgnoreCase));

        if (!alreadyIncluded)
            images.Add(new { Id = 0, ImageUrl = media.Image, SortOrder = -1, Source = "media" });
    }

    return Results.Ok(images);
});

// ============================================================
// POST /api/v1/admin/pois/{id}/images — Thêm ảnh (URL ngoài hoặc path nội bộ)
// Body: { imageUrl, sortOrder? }
// ============================================================
app.MapPost("/api/v1/admin/pois/{id:int}/images", async (
    int id, PoiImageAddRequest req, AppDb db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.ImageUrl))
        return Results.BadRequest(new { error = "ImageUrl là bắt buộc" });

    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    if (poi is null) return Results.NotFound(new { error = "POI không tồn tại" });

    var img = new MapApi.Models.PoiImage
    {
        IdPoi     = id,
        ImageUrl  = req.ImageUrl.Trim(),
        SortOrder = req.SortOrder ?? 0,
        CreatedAt = DateTime.UtcNow
    };
    db.PoiImages.Add(img);
    await db.SaveChangesAsync(ct);

    return Results.Ok(new { ok = true, id = img.Id, imageUrl = img.ImageUrl });
});

// DELETE /api/v1/admin/pois/{id}/images/{imageId} — Xóa ảnh
app.MapDelete("/api/v1/admin/pois/{id:int}/images/{imageId:long}", async (
    int id, long imageId, AppDb db, CancellationToken ct) =>
{
    var img = await db.PoiImages.FirstOrDefaultAsync(x => x.Id == imageId && x.IdPoi == id, ct);
    if (img is null) return Results.NotFound();
    db.PoiImages.Remove(img);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { ok = true });
});

// ============================================================
// GET /api/v1/pois/search?q={keyword} — Tìm kiếm POI theo tên/mô tả
// ============================================================
app.MapGet("/api/v1/pois/search", async (string? q, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.Ok(Array.Empty<object>());

    var keyword = q.Trim().ToLower();
    var results = await db.Pois.AsNoTracking()
        .Where(p => p.IsActive &&
                    (p.Name.ToLower().Contains(keyword) ||
                     (p.Description != null && p.Description.ToLower().Contains(keyword))))
        .OrderBy(p => p.Name)
        .Take(10)
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Description,
            p.Latitude,
            p.Longitude,
            p.RadiusMeters
        })
        .ToListAsync();

    return Results.Ok(results);
})
.WithName("SearchPois")
.WithSummary("Tìm kiếm POI theo từ khóa (tên hoặc mô tả)");

// ============================================================
// POST /api/access/check-poi — Kiểm tra quyền truy cập POI
// Body: { poiId, deviceId? }   Header: Bearer token (tùy chọn)
// ============================================================
app.MapPost("/api/access/check-poi", async (
    AccessCheckPoiRequest req, HttpContext ctx, AppDb db, CancellationToken ct) =>
{
    if (req.PoiId <= 0)
        return Results.BadRequest(new { error = "PoiId không hợp lệ" });

    Guid? userId = null;
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Guid.TryParse(idClaim, out var uid)) userId = uid;

    var userParam   = new Microsoft.Data.SqlClient.SqlParameter("@UserId",   (object?)userId   ?? DBNull.Value);
    var deviceParam = new Microsoft.Data.SqlClient.SqlParameter("@DeviceId", (object?)req.DeviceId ?? DBNull.Value);
    var poiParam    = new Microsoft.Data.SqlClient.SqlParameter("@PoiId",    req.PoiId);

    var rows = await db.Set<AccessCheckRow>()
        .FromSqlRaw("EXEC dbo.CanAccessPoi @UserId, @DeviceId, @PoiId",
            userParam, deviceParam, poiParam)
        .AsNoTracking()
        .ToListAsync(ct);

    var row = rows.FirstOrDefault();
    if (row is null)
        return Results.Problem("Không thể kiểm tra quyền truy cập.");

    if (!row.AccessGranted)
        return Results.Json(new
        {
            accessGranted    = false,
            accessReason     = row.AccessReason,
            remainingFreeUses = 0,
            showPaywall      = true,
            poiId            = row.PoiId
        }, statusCode: 402);

    return Results.Ok(new
    {
        accessGranted     = true,
        accessReason      = row.AccessReason,
        remainingFreeUses = row.RemainingFreeUses,
        matchedAreaId     = row.MatchedAreaId,
        poiId             = row.PoiId
    });
});

// ============================================================
// POST /api/usage/consume-poi-listen — Ghi nhận sự kiện POI_LISTEN
// Chỉ gọi khi đã có quyền truy cập (sau check-poi trả về 200)
// Body: { poiId, areaId?, deviceId?, metadataJson? }
// ============================================================
app.MapPost("/api/usage/consume-poi-listen", async (
    ConsumePoisListenRequest req, HttpContext ctx, AppDb db, CancellationToken ct) =>
{
    if (req.PoiId <= 0)
        return Results.BadRequest(new { error = "PoiId không hợp lệ" });

    Guid? userId = null;
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Guid.TryParse(idClaim, out var uid)) userId = uid;

    if (userId is null && string.IsNullOrWhiteSpace(req.DeviceId))
        return Results.BadRequest(new { error = "Cần UserId (Bearer) hoặc DeviceId" });

    var userParam     = new Microsoft.Data.SqlClient.SqlParameter("@UserId",       (object?)userId     ?? DBNull.Value);
    var deviceParam   = new Microsoft.Data.SqlClient.SqlParameter("@DeviceId",     (object?)req.DeviceId ?? DBNull.Value);
    var poiParam      = new Microsoft.Data.SqlClient.SqlParameter("@PoiId",        req.PoiId);
    var areaParam     = new Microsoft.Data.SqlClient.SqlParameter("@AreaId",       (object?)req.AreaId ?? DBNull.Value);
    var metaParam     = new Microsoft.Data.SqlClient.SqlParameter("@MetadataJson", (object?)req.MetadataJson ?? DBNull.Value);

    await db.Database.ExecuteSqlRawAsync(
        "EXEC dbo.ConsumePoisListenUsage @UserId, @DeviceId, @PoiId, @AreaId, @MetadataJson",
        [userParam, deviceParam, poiParam, areaParam, metaParam], ct);

    return Results.Ok(new { ok = true });
});

// ============================================================
// GET /api/me/usage-status — Trạng thái quota hiện tại
// Header: Bearer token (user) HOẶC query ?deviceId=... (guest)
// ============================================================
app.MapGet("/api/me/usage-status", async (
    string? deviceId, HttpContext ctx, AppDb db, CancellationToken ct) =>
{
    Guid? userId = null;
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (Guid.TryParse(idClaim, out var uid)) userId = uid;

    if (userId is null && string.IsNullOrWhiteSpace(deviceId))
        return Results.BadRequest(new { error = "Cần Bearer token hoặc ?deviceId=..." });

    var userParam   = new Microsoft.Data.SqlClient.SqlParameter("@UserId",   (object?)userId   ?? DBNull.Value);
    var deviceParam = new Microsoft.Data.SqlClient.SqlParameter("@DeviceId", (object?)deviceId ?? DBNull.Value);

    var rows = await db.Set<UsageStatusRow>()
        .FromSqlRaw("EXEC dbo.GetUsageStatus @UserId, @DeviceId", userParam, deviceParam)
        .AsNoTracking()
        .ToListAsync(ct);

    var row = rows.FirstOrDefault();
    if (row is null) return Results.Problem("Không lấy được trạng thái quota.");

    return Results.Ok(new
    {
        usedLast24h         = row.UsedLast24h,
        remainingFreeUses   = row.RemainingFreeUses ?? 0,
        hasActivePro        = row.HasActivePro,
        isFreeLimitExceeded = row.IsFreeLimitExceeded,
        resetInfo           = row.IsFreeLimitExceeded
            ? "Lượt sẽ được làm mới sau 24 giờ kể từ lần dùng đầu tiên"
            : null
    });
});

// ============================================================
// GET /api/me/entitlements — Danh sách gói đang active
// Yêu cầu Bearer token
// ============================================================
app.MapGet("/api/me/entitlements", async (
    HttpContext ctx, AppDb db, CancellationToken ct) =>
{
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    var userParam = new Microsoft.Data.SqlClient.SqlParameter("@UserId", userId);

    var rows = await db.Set<UserEntitlementRow>()
        .FromSqlRaw("EXEC dbo.GetUserEntitlements @UserId", userParam)
        .AsNoTracking()
        .ToListAsync(ct);

    return Results.Ok(rows.Select(r => new
    {
        r.EntitlementId,
        r.ProductCode,
        r.ProductName,
        r.ProductType,
        r.EntitlementType,
        r.StartsAt,
        r.ExpiresAt,
        r.Status,
        r.IsValid,
        areaCodes = r.AreaCodes?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [],
        areaIds   = r.AreaIds?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(int.Parse).ToArray() ?? []
    }));
}).RequireAuthorization();

// ============================================================
// GET /api/v1/products/areas — Danh sách khu vực kèm sản phẩm (MAUI)
// ============================================================
app.MapGet("/api/v1/products/areas", async (AppDb db, CancellationToken ct) =>
{
    var now = DateTime.UtcNow;
    var data = await (
        from p in db.Products
        join pa in db.ProductAreas on p.ProductId equals pa.ProductId
        join a in db.Areas on pa.AreaId equals a.AreaId
        where p.IsActive && a.IsActive && p.ProductType == "AREA_PACK"
        orderby a.Name
        select new
        {
            a.AreaId,
            AreaCode    = a.Code,
            AreaName    = a.Name,
            City        = a.City ?? a.Name,
            p.ProductCode,
            p.Price,
            DurationHours = p.DurationHours ?? 24
        }
    ).AsNoTracking().ToListAsync(ct);

    return Results.Ok(data);
});

// ============================================================
// POST /api/v1/payments/purchase — Mua gói (demo: grant ngay)
// Body: { productCode, paymentMethod }
// ============================================================
app.MapPost("/api/v1/payments/purchase", async (
    PurchaseRequest req, HttpContext ctx, AppDb db, CancellationToken ct) =>
{
    var idClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(req.ProductCode))
        return Results.BadRequest(new { error = "ProductCode là bắt buộc" });

    var product = await db.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.ProductCode == req.ProductCode && p.IsActive, ct);

    if (product is null)
        return Results.NotFound(new { error = "Sản phẩm không tồn tại hoặc đã ngừng bán" });

    var now      = DateTime.UtcNow;
    var expiresAt = product.DurationHours.HasValue
        ? now.AddHours(product.DurationHours.Value)
        : (DateTime?)null;

    // Ghi transaction
    db.PurchaseTransactions.Add(new PurchaseTransaction
    {
        UserId          = userId,
        ProductId       = product.ProductId,
        Amount          = product.Price,
        Currency        = product.Currency,
        PaymentProvider = req.PaymentMethod ?? "DEMO",
        PaymentRef      = $"DEMO-{Guid.NewGuid():N}",
        Status          = "PAID",
        PaidAt          = now
    });

    // Cấp entitlement
    db.UserEntitlements.Add(new UserEntitlement
    {
        UserId          = userId,
        ProductId       = product.ProductId,
        EntitlementType = product.ProductType,
        StartsAt        = now,
        ExpiresAt       = expiresAt,
        Status          = "ACTIVE"
    });

    // Nếu PRO → cập nhật Users.PlanType
    if (product.ProductType == "PRO")
    {
        var user = await db.Users.FindAsync([userId], ct);
        if (user is not null)
        {
            user.PlanType      = "PRO";
            user.ProExpiryDate = expiresAt;
        }
    }

    await db.SaveChangesAsync(ct);

    return Results.Ok(new
    {
        success     = true,
        packageName = product.ProductName,
        productType = product.ProductType,
        expiresAt
    });
}).RequireAuthorization();

// ============================================================
// GET /api/v1/plans — Danh sách tất cả gói sản phẩm
// ============================================================
app.MapGet("/api/v1/plans", async (AppDb db, CancellationToken ct) =>
{
    var products = await db.Products.AsNoTracking()
        .Where(p => p.IsActive)
        .OrderBy(p => p.ProductType).ThenBy(p => p.Price)
        .Select(p => new
        {
            p.ProductId, p.ProductCode, p.ProductName, p.ProductType,
            p.Price, p.Currency, p.DurationHours, p.IsActive,
            p.UnlockNarration, p.UnlockLanguages, p.UnlockQr, p.UnlockOffline
        })
        .ToListAsync(ct);
    return Results.Ok(products);
});

// ============================================================
// GET /api/v1/areas — Danh sách khu vực active (public)
// ============================================================
app.MapGet("/api/v1/areas", async (AppDb db, CancellationToken ct) =>
{
    var areas = await db.Areas.AsNoTracking()
        .Where(a => a.IsActive)
        .OrderBy(a => a.Name)
        .Select(a => new
        {
            a.AreaId, a.Code, a.Name, a.City, a.Province, a.Description
        })
        .ToListAsync(ct);
    return Results.Ok(areas);
});

// ============================================================
// GET /api/v1/admin/revenue — Báo cáo doanh thu (Admin)
// Query: ?from=yyyy-MM-dd&to=yyyy-MM-dd&areaCode=...
// ============================================================
app.MapGet("/api/v1/admin/revenue", async (
    string? from, string? to, string? areaCode, AppDb db, CancellationToken ct) =>
{
    DateTime? fromDate = DateTime.TryParse(from, out var fd) ? fd.ToUniversalTime() : null;
    DateTime? toDate   = DateTime.TryParse(to,   out var td) ? td.ToUniversalTime().AddDays(1) : null;

    var q = db.PurchaseTransactions.AsNoTracking()
        .Where(t => t.Status == "PAID");
    if (fromDate.HasValue) q = q.Where(t => t.PaidAt >= fromDate);
    if (toDate.HasValue)   q = q.Where(t => t.PaidAt <  toDate);

    var txns = await (
        from t in q
        join p in db.Products on t.ProductId equals p.ProductId
        select new { t.TransactionId, t.UserId, t.Amount, t.Currency, t.PaidAt,
                     p.ProductCode, p.ProductName, p.ProductType }
    ).ToListAsync(ct);

    // Lọc theo khu vực nếu có
    if (!string.IsNullOrWhiteSpace(areaCode))
    {
        var areaPackIds = await (
            from p  in db.Products
            join pa in db.ProductAreas on p.ProductId equals pa.ProductId
            join a  in db.Areas        on pa.AreaId   equals a.AreaId
            where a.Code == areaCode
            select p.ProductCode
        ).AsNoTracking().ToListAsync(ct);

        txns = txns.Where(t => areaPackIds.Contains(t.ProductCode)).ToList();
    }

    var byProduct = txns
        .GroupBy(t => new { t.ProductCode, t.ProductName, t.ProductType })
        .Select(g => new
        {
            g.Key.ProductCode, g.Key.ProductName, g.Key.ProductType,
            TotalRevenue = g.Sum(t => t.Amount),
            TotalOrders  = g.Count()
        })
        .OrderByDescending(x => x.TotalRevenue)
        .ToList();

    var summary = new
    {
        TotalRevenue     = txns.Sum(t => t.Amount),
        TotalOrders      = txns.Count,
        TotalProRevenue  = txns.Where(t => t.ProductType == "PRO").Sum(t => t.Amount),
        TotalAreaRevenue = txns.Where(t => t.ProductType == "AREA_PACK").Sum(t => t.Amount),
        ByProduct        = byProduct
    };

    return Results.Ok(summary);
});

// ============================================================
// GET /api/v1/admin/products — Quản lý sản phẩm (Admin)
// ============================================================
app.MapGet("/api/v1/admin/products", async (AppDb db, CancellationToken ct) =>
{
    var products = await (
        from p in db.Products
        let areaCount = db.ProductAreas.Count(pa => pa.ProductId == p.ProductId)
        orderby p.ProductType, p.Price
        select new
        {
            p.ProductId, p.ProductCode, p.ProductName, p.ProductType,
            p.Price, p.Currency, p.DurationHours, p.IsActive, p.CreatedAt,
            p.UnlockNarration, p.UnlockLanguages, p.UnlockQr, p.UnlockOffline,
            AreaCount = areaCount
        }
    ).AsNoTracking().ToListAsync(ct);
    return Results.Ok(products);
});

// PUT /api/v1/admin/products/{id} — Chỉnh sửa sản phẩm
app.MapPut("/api/v1/admin/products/{id:int}", async (
    int id, AdminProductUpdateRequest req, AppDb db, CancellationToken ct) =>
{
    var product = await db.Products.FirstOrDefaultAsync(p => p.ProductId == id, ct);
    if (product is null) return Results.NotFound(new { error = "Sản phẩm không tồn tại" });

    if (!string.IsNullOrWhiteSpace(req.ProductName)) product.ProductName = req.ProductName;
    if (req.Price.HasValue)        product.Price        = req.Price.Value;
    if (req.IsActive.HasValue)     product.IsActive     = req.IsActive.Value;
    if (req.DurationHours.HasValue) product.DurationHours = req.DurationHours.Value;

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { ok = true });
});

// ============================================================
// GET /api/v1/admin/areas — Quản lý khu vực (Admin)
// ============================================================
app.MapGet("/api/v1/admin/areas", async (AppDb db, CancellationToken ct) =>
{
    var areas = await (
        from a in db.Areas
        let poiCount     = db.AreaPois.Count(ap => ap.AreaId == a.AreaId)
        let productCount = db.ProductAreas.Count(pa => pa.AreaId == a.AreaId)
        orderby a.Name
        select new
        {
            a.AreaId, a.Code, a.Name, a.City, a.Province,
            a.Description, a.IsActive, a.CreatedAt,
            PoiCount = poiCount, ProductCount = productCount
        }
    ).AsNoTracking().ToListAsync(ct);
    return Results.Ok(areas);
});

// ============================================================
// GET /api/v1/admin/pois/{id}/areas — Lấy danh sách khu vực của POI
// ============================================================
app.MapGet("/api/v1/admin/pois/{id:int}/areas", async (int id, AppDb db, CancellationToken ct) =>
{
    var areas = await (
        from ap in db.AreaPois
        join a  in db.Areas on ap.AreaId equals a.AreaId
        where ap.PoiId == id
        orderby ap.IsPrimaryArea descending, ap.SortOrder
        select new
        {
            a.AreaId, a.Code, a.Name, a.City,
            ap.SortOrder, ap.IsPrimaryArea
        }
    ).AsNoTracking().ToListAsync(ct);
    return Results.Ok(areas);
});

// ============================================================
// POST /api/v1/admin/pois/{id}/areas — Gán khu vực cho POI
// ============================================================
app.MapPost("/api/v1/admin/pois/{id:int}/areas", async (
    int id, PoiAreaAssignRequest req, AppDb db, CancellationToken ct) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    if (poi is null) return Results.NotFound(new { error = "POI không tồn tại" });

    var area = await db.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.AreaId == req.AreaId, ct);
    if (area is null) return Results.NotFound(new { error = "Khu vực không tồn tại" });

    var existing = await db.AreaPois
        .FirstOrDefaultAsync(ap => ap.AreaId == req.AreaId && ap.PoiId == id, ct);

    if (existing is not null)
    {
        existing.SortOrder     = req.SortOrder;
        existing.IsPrimaryArea = req.IsPrimaryArea;
    }
    else
    {
        db.AreaPois.Add(new MapApi.Models.AreaPoi
        {
            AreaId        = req.AreaId,
            PoiId         = id,
            SortOrder     = req.SortOrder,
            IsPrimaryArea = req.IsPrimaryArea
        });
    }

    // Nếu đặt là Primary thì clear các area primary khác của POI này
    if (req.IsPrimaryArea)
    {
        var others = await db.AreaPois
            .Where(ap => ap.PoiId == id && ap.AreaId != req.AreaId && ap.IsPrimaryArea)
            .ToListAsync(ct);
        foreach (var o in others) o.IsPrimaryArea = false;
    }

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { ok = true, areaId = req.AreaId, poiId = id });
});

// DELETE /api/v1/admin/pois/{id}/areas/{areaId} — Xóa gán khu vực
app.MapDelete("/api/v1/admin/pois/{id:int}/areas/{areaId:int}", async (
    int id, int areaId, AppDb db, CancellationToken ct) =>
{
    var entry = await db.AreaPois
        .FirstOrDefaultAsync(ap => ap.PoiId == id && ap.AreaId == areaId, ct);
    if (entry is null) return Results.NotFound();
    db.AreaPois.Remove(entry);
    await db.SaveChangesAsync(ct);
    return Results.Ok(new { ok = true });
});

// ============================================================
// GET /api/v1/admin/users/{id} — Chi tiết user: plan + entitlements + lịch sử mua
// ============================================================
app.MapGet("/api/v1/admin/users/{id:guid}", async (Guid id, AppDb db, CancellationToken ct) =>
{
    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id, ct);
    if (user is null) return Results.NotFound();

    var entitlements = await (
        from e in db.UserEntitlements
        join p in db.Products on e.ProductId equals p.ProductId
        where e.UserId == id
        orderby e.StartsAt descending
        select new
        {
            e.EntitlementId, p.ProductCode, p.ProductName, p.ProductType,
            e.EntitlementType, e.StartsAt, e.ExpiresAt, e.Status,
            IsValid = e.Status == "ACTIVE" && (e.ExpiresAt == null || e.ExpiresAt > DateTime.UtcNow)
        }
    ).AsNoTracking().ToListAsync(ct);

    var transactions = await (
        from t in db.PurchaseTransactions
        join p in db.Products on t.ProductId equals p.ProductId
        where t.UserId == id
        orderby t.PaidAt descending
        select new
        {
            t.TransactionId, p.ProductCode, p.ProductName, p.ProductType,
            t.Amount, t.Currency, t.PaymentProvider, t.PaymentRef, t.Status, t.PaidAt
        }
    ).AsNoTracking().Take(20).ToListAsync(ct);

    return Results.Ok(new
    {
        user.UserId, user.Username, user.Mail, user.PhoneNumber,
        user.AvatarUrl, user.IsActive, user.CreatedAt,
        user.PlanType, user.ProExpiryDate,
        Entitlements = entitlements,
        PurchaseHistory = transactions
    });
});

// ============================================================
// POST /api/v1/payment/callback — Webhook thanh toán từ payment gateway
// Body: { paymentRef, status, provider, amount, currency }
// ============================================================
app.MapPost("/api/v1/payment/callback", async (
    PaymentCallbackRequest req, AppDb db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.PaymentRef))
        return Results.BadRequest(new { error = "PaymentRef là bắt buộc" });

    var txn = await db.PurchaseTransactions
        .FirstOrDefaultAsync(t => t.PaymentRef == req.PaymentRef, ct);

    if (txn is null)
        return Results.NotFound(new { error = "Không tìm thấy giao dịch" });

    if (txn.Status == "PAID")
        return Results.Ok(new { ok = true, message = "Giao dịch đã được xử lý trước đó" });

    var newStatus = req.Status.ToUpperInvariant() == "SUCCESS" ? "PAID" : "FAILED";
    txn.Status = newStatus;
    if (newStatus == "PAID") txn.PaidAt = DateTime.UtcNow;

    if (newStatus == "PAID")
    {
        var product = await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProductId == txn.ProductId, ct);

        if (product is not null)
        {
            var now       = DateTime.UtcNow;
            var expiresAt = product.DurationHours.HasValue
                ? now.AddHours(product.DurationHours.Value) : (DateTime?)null;

            db.UserEntitlements.Add(new MapApi.Models.UserEntitlement
            {
                UserId          = txn.UserId,
                ProductId       = product.ProductId,
                EntitlementType = product.ProductType,
                StartsAt        = now,
                ExpiresAt       = expiresAt,
                Status          = "ACTIVE"
            });

            if (product.ProductType == "PRO")
            {
                var user = await db.Users.FindAsync([txn.UserId], ct);
                if (user is not null)
                {
                    user.PlanType      = "PRO";
                    user.ProExpiryDate = expiresAt;
                }
            }
        }
    }

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { ok = true, status = newStatus });
});

app.MapHub<MapApi.Hubs.DeviceHub>("/hubs/device");
app.MapControllers();
app.Run();
