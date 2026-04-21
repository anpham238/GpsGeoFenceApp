using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
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
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

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
            IssuerSigningKey = jwtKey,
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
builder.Services.AddHostedService<TranslationBackgroundService>();
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
// Near  (evt=1) → "[gần đến Name]. NarTTS"
// Enter (evt=0) / Tap (evt=2) → "[đã đến Name]. NarTTS. Description"
// ============================================================

// Lời mở đầu đa ngôn ngữ — Near
var NearPrefix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["vi-VN"]  = "Bạn sắp đến {0}.",
    ["en-US"]  = "You are approaching {0}.",
    ["zh-Hans"]= "您即将到达{0}。",
    ["ja-JP"]  = "{0}に近づいています。",
    ["ko-KR"]  = "{0}에 가까워지고 있습니다.",
    ["de-DE"]  = "Sie nähern sich {0}.",
};

// Lời mở đầu đa ngôn ngữ — Enter / Tap
var EnterPrefix = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["vi-VN"]  = "Bạn đã đến {0}.",
    ["en-US"]  = "You have arrived at {0}.",
    ["zh-Hans"]= "您已到达{0}。",
    ["ja-JP"]  = "{0}に到着しました。",
    ["ko-KR"]  = "{0}에 도착하셨습니다.",
    ["de-DE"]  = "Sie sind in {0} angekommen.",
};

app.MapGet("/api/v1/pois/{id}/narration", async (
    int id, string? lang, string? eventType,
    HttpContext ctx, AppDb db, CancellationToken ct) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });

    var toLang = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang.Trim();
    var evt    = ParseEventTypeByte(eventType);

    // Gate ngôn ngữ premium
    var langInfo = await db.SupportedLanguages.AsNoTracking()
        .FirstOrDefaultAsync(l => l.LanguageTag == toLang && l.IsActive, ct);
    if (langInfo?.IsPremium == true)
    {
        var idClaimNar = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
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

    // Fallback về vi-VN nếu ngôn ngữ yêu cầu chưa được dịch
    if (poiLang is null && toLang != "vi-VN")
        poiLang = await db.PoiLanguages.AsNoTracking()
            .FirstOrDefaultAsync(n => n.IdPoi == id && n.LanguageTag == "vi-VN", ct);

    // TextToSpeech đã chứa NarTTS + Description kết hợp (lưu lúc dịch)
    var tts  = poiLang?.TextToSpeech ?? "";
    var name = poi.Name;  // tên gốc — proper noun, dùng chung mọi ngôn ngữ

    var prefixDict = evt == 1 ? NearPrefix : EnterPrefix;
    var template   = prefixDict.TryGetValue(toLang, out var t) ? t : prefixDict["vi-VN"];
    var prefix     = string.Format(template, name);
    var narText    = string.IsNullOrWhiteSpace(tts) ? prefix : $"{prefix} {tts}";

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
app.MapPost("/api/v1/auth/register", async (HttpContext ctx, AppDb db, IWebHostEnvironment env) => {
    var request = ctx.Request; // Thêm dòng này vào để bóc request ra
    if (!request.HasFormContentType)
        return Results.BadRequest(new { error = "Yêu cầu multipart/form-data" });
    var form = await request.ReadFormAsync();
    var username = form["Username"].ToString().Trim();
    var mail     = form["Mail"].ToString().Trim().ToLowerInvariant();
    var password = form["Password"].ToString();
    var phone    = form["PhoneNumber"].ToString().Trim();

    if (string.IsNullOrWhiteSpace(username) ||
        string.IsNullOrWhiteSpace(mail) ||
        string.IsNullOrWhiteSpace(password))
        return Results.BadRequest(new { error = "Username, Mail và Password là bắt buộc" });

    if (await db.Users.AnyAsync(u => u.Username == username))
        return Results.Conflict(new { error = "Username đã tồn tại" });
    if (await db.Users.AnyAsync(u => u.Mail == mail))
        return Results.Conflict(new { error = "Email đã được đăng ký" });

    var avatarUrl = "default-avatar.png";
    var avatarFile = form.Files.GetFile("Avatar");
    if (avatarFile is { Length: > 0 })
    {
        var ext = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png")
        {
            var dir = Path.Combine(env.WebRootPath, "avatars");
            Directory.CreateDirectory(dir);
            var safeName = $"{Guid.NewGuid():N}{ext}";
            await using var fs = System.IO.File.Create(Path.Combine(dir, safeName));
            await avatarFile.CopyToAsync(fs);
            avatarUrl = $"/avatars/{safeName}";
        }
    }

    var user = new Users
    {
        UserId       = Guid.NewGuid(),
        Username     = username,
        Mail         = mail,
        PhoneNumber  = string.IsNullOrWhiteSpace(phone) ? null : phone,
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
        AvatarUrl    = avatarUrl,
        IsActive     = true,
        CreatedAt    = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/auth/me", new { user.UserId, user.Username, user.Mail, user.AvatarUrl });
});

// ============================================================
// POST /api/v1/auth/login  — Smart Login (Username hoặc Email)
// ============================================================
app.MapPost("/api/v1/auth/login", async (LoginRequest req, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.Identifier) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Identifier và Password là bắt buộc" });

    var identifier = req.Identifier.Trim();
    Users? user;

    if (identifier.Contains('@'))
        user = await db.Users.FirstOrDefaultAsync(u => u.Mail == identifier.ToLowerInvariant() && u.IsActive);
    else
        user = await db.Users.FirstOrDefaultAsync(u => u.Username == identifier && u.IsActive);

    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var token = GenerateJwtToken(user, jwtKey);

    return Results.Ok(new
    {
        Token     = token,
        UserId    = user.UserId,
        user.Username,
        user.Mail,
        user.AvatarUrl,
        user.PlanType,
        user.ProExpiryDate,
        ExpiresAt = DateTime.UtcNow.AddHours(24)
    });
});

// ============================================================
// GET /api/v1/auth/me  — Lấy profile từ JWT
// ============================================================
app.MapGet("/api/v1/auth/me", async (HttpContext ctx, AppDb db) =>
{
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId))
        return Results.Unauthorized();

    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
    if (user is null) return Results.NotFound(new { error = "User not found" });

    return Results.Ok(new
    {
        user.UserId,
        user.Username,
        user.Mail,
        user.PhoneNumber,
        user.AvatarUrl,
        user.PlanType,
        user.ProExpiryDate,
        user.CreatedAt
    });
}).RequireAuthorization();

// ============================================================
// PUT /api/v1/profile — Cập nhật thông tin cá nhân
// ============================================================
app.MapPut("/api/v1/profile", async (HttpContext ctx, AppDb db, IWebHostEnvironment env) =>
{
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
    if (user is null) return Results.NotFound(new { error = "User not found" });

    if (!ctx.Request.HasFormContentType)
        return Results.BadRequest(new { error = "Yêu cầu multipart/form-data" });

    var form = await ctx.Request.ReadFormAsync();
    var username = form["Username"].ToString().Trim();
    var phone    = form["PhoneNumber"].ToString().Trim();

    if (!string.IsNullOrWhiteSpace(username) && username != user.Username)
    {
        if (await db.Users.AnyAsync(u => u.Username == username && u.UserId != userId))
            return Results.Conflict(new { error = "Username đã tồn tại" });
        user.Username = username;
    }

    if (!string.IsNullOrWhiteSpace(phone))
        user.PhoneNumber = phone;

    var avatarFile = form.Files.GetFile("Avatar");
    if (avatarFile is { Length: > 0 })
    {
        var ext = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg" or ".png")
        {
            var dir = Path.Combine(env.WebRootPath, "avatars");
            Directory.CreateDirectory(dir);
            var safeName = $"{Guid.NewGuid():N}{ext}";
            await using var fs = System.IO.File.Create(Path.Combine(dir, safeName));
            await avatarFile.CopyToAsync(fs);
            user.AvatarUrl = $"/avatars/{safeName}";
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(new { user.UserId, user.Username, user.Mail, user.PhoneNumber, user.AvatarUrl, user.PlanType });
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
app.MapPost("/api/v1/profile/upgrade", async (HttpContext ctx, AppDb db) =>
{
    var idClaim = ctx.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    if (!Guid.TryParse(idClaim, out var userId)) return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive);
    if (user is null) return Results.NotFound();

    user.PlanType = "PRO";
    user.ProExpiryDate = DateTime.UtcNow.AddDays(30);
    await db.SaveChangesAsync();

    return Results.Ok(new { ok = true, PlanType = user.PlanType, ProExpiryDate = user.ProExpiryDate });
}).RequireAuthorization();

// ============================================================
// POST /api/v1/history — Ghi lịch sử ghé thăm (thay /api/v1/playback)
// ============================================================
app.MapPost("/api/v1/history", async (HistoryRequest req, AppDb db) =>
{
    if (req.PoiId <= 0 || req.UserId == Guid.Empty)
        return Results.BadRequest(new { error = "PoiId và UserId là bắt buộc" });

    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.PoiId);
    if (poi is null) return Results.BadRequest(new { error = "POI not found" });

    if (!await db.Users.AnyAsync(u => u.UserId == req.UserId))
        return Results.BadRequest(new { error = "User not found" });

    // Upsert theo (IdPoi, IdUser)
    var existing = await db.HistoryPoi
        .FirstOrDefaultAsync(h => h.IdPoi == req.PoiId && h.IdUser == req.UserId);

    if (existing is not null)
    {
        existing.Quantity++;
        existing.LastVisitedAt = DateTime.UtcNow;
        existing.TotalDurationSeconds =
            (existing.TotalDurationSeconds ?? 0) + (req.DurationSeconds ?? 0);
    }
    else
    {
        db.HistoryPoi.Add(new HistoryPoi
        {
            IdPoi = req.PoiId,
            IdUser = req.UserId,
            PoiName = poi.Name,
            Quantity = 1,
            LastVisitedAt = DateTime.UtcNow,
            TotalDurationSeconds = req.DurationSeconds
        });
    }

    await db.SaveChangesAsync();
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
            p.RadiusMeters, p.CooldownSeconds, p.IsActive, p.CreatedAt, p.UpdatedAt
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
    poi.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
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
app.MapGet("/api/v1/admin/stats", async (AppDb db) =>
{
    var timeout = DateTime.UtcNow.AddMinutes(-5);
    var totalDevices  = await db.GuestDevices.CountAsync();
    var onlineDevices = await db.GuestDevices.CountAsync(d => d.LastActiveAt >= timeout);
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
app.MapGet("/api/v1/admin/devices", async (AppDb db) =>
{
    var timeout = DateTime.UtcNow.AddMinutes(-5);
    var devices = await db.GuestDevices.AsNoTracking()
        .OrderByDescending(d => d.LastActiveAt)
        .Select(d => new
        {
            d.DeviceId, d.Platform, d.AppVersion,
            d.LastLatitude, d.LastLongitude,
            d.FirstSeenAt, d.LastActiveAt,
            IsOnline = d.LastActiveAt >= timeout
        })
        .ToListAsync();
    return Results.Ok(devices);
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

    var maxLimit = actionType == "QR_SCAN" ? 5 : 10;
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
        return Results.Ok(new { allowed = true, used = 1, limit = maxLimit, resetIn = "12h" });
    }

    // Reset sau 12 giờ
    if ((now - tracking.LastResetAt).TotalHours >= 12)
    {
        tracking.UsedCount = 1;
        tracking.LastResetAt = now;
        await db.SaveChangesAsync();
        return Results.Ok(new { allowed = true, used = 1, limit = maxLimit, resetIn = "12h" });
    }

    if (tracking.UsedCount < maxLimit)
    {
        tracking.UsedCount++;
        await db.SaveChangesAsync();
        var resetInHours = 12 - (now - tracking.LastResetAt).TotalHours;
        return Results.Ok(new { allowed = true, used = tracking.UsedCount, limit = maxLimit, resetInHours });
    }

    // Hết lượt → 402
    var timeLeft = 12 - (now - tracking.LastResetAt).TotalHours;
    return Results.Json(
        new { allowed = false, used = tracking.UsedCount, limit = maxLimit, resetInHours = timeLeft,
              message = $"Hết lượt. Làm mới sau {timeLeft:F1} giờ nữa." },
        statusCode: 402);
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

app.MapHub<MapApi.Hubs.DeviceHub>("/hubs/device");
app.MapControllers();
app.Run();
// ─── Helpers ───────────────────────────────────────────────────────────────
static byte ParseEventTypeByte(string? s) =>
    (s ?? "").Trim().ToLowerInvariant() switch
    {
        "enter" => 0,
        "near" => 1,
        "tap" => 2,
        _ => 0
    };
static string GenerateJwtToken(Users user, SymmetricSecurityKey key)
{
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new Claim(ClaimTypes.Name, user.Username),
        new Claim(ClaimTypes.Email, user.Mail)
    };
    var token = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.UtcNow.AddHours(24),
        signingCredentials: creds);
    return new JwtSecurityTokenHandler().WriteToken(token);
}

// ─── Request DTOs ───────────────────────────────────────────────────────────
public sealed record LoginRequest(string Identifier, string Password);
public sealed class HistoryRequest
{
    public int PoiId { get; set; }
    public Guid UserId { get; set; }
    public int? DurationSeconds { get; set; }
}
public sealed class PoiUpdateRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? RadiusMeters { get; set; }
    public int? CooldownSeconds { get; set; }
}
public sealed record AnalyticsVisitRequest(Guid SessionId, int PoiId, string Action);
public sealed record AnalyticsRouteRequest(Guid SessionId, double Latitude, double Longitude);
public sealed record AnalyticsListenRequest(Guid SessionId, int PoiId, int DurationSeconds);
public sealed record AdminUserUpdateRequest(string? PhoneNumber, string? PlanType, DateTime? ProExpiryDate);
public sealed record UsageCheckRequest(string EntityId, string ActionType);
