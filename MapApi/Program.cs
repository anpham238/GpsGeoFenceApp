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
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));
builder.Services.AddHttpClient<TranslatorClient>();
builder.Services.AddScoped<PoiManagementService>();
builder.Services.AddHostedService<TranslationBackgroundService>();

var app = builder.Build();

app.UseCors();
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
// GET /api/v1/pois — Trả về POI kèm media đầu tiên (backward compat)
// ============================================================
app.MapGet("/api/v1/pois", async (AppDb db) =>
{
    var pois = await db.Pois.AsNoTracking()
        .Where(p => p.IsActive)
        .OrderBy(p => p.Name)
        .ToListAsync();

    // Join PoiMedia để lấy image/maplink đầu tiên cho mỗi POI
    var poiIds = pois.Select(p => p.Id).ToList();
    var mediaMap = await db.PoiMedia.AsNoTracking()
        .Where(m => poiIds.Contains(m.IdPoi))
        .GroupBy(m => m.IdPoi)
        .Select(g => new { IdPoi = g.Key, g.First().Image, g.First().MapLink, g.First().Audio })
        .ToDictionaryAsync(x => x.IdPoi);

    var result = pois.Select(p => new
    {
        p.Id,
        p.Name,
        p.Description,
        p.Latitude,
        p.Longitude,
        p.RadiusMeters,
        p.CooldownSeconds,
        p.IsActive,
        p.UpdatedAt,
        // Backward compat cho mobile
        NearRadiusMeters = p.RadiusMeters * 2,
        DebounceSeconds = 3,
        ImageUrl  = mediaMap.TryGetValue(p.Id, out var m)  ? m.Image   : null,
        MapLink   = mediaMap.TryGetValue(p.Id, out var m2) ? m2.MapLink : null,
        AudioUrl  = mediaMap.TryGetValue(p.Id, out var m3) ? m3.Audio   : null,
        Language = "vi-VN",
        NarrationText = (string?)null  // mobile sẽ fetch qua /narration endpoint
    });

    return Results.Ok(result);
});

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
        poi.Id,
        poi.Name,
        poi.Description,
        poi.Latitude,
        poi.Longitude,
        poi.RadiusMeters,
        poi.CooldownSeconds,
        poi.IsActive,
        poi.UpdatedAt,
        NearRadiusMeters = poi.RadiusMeters * 2,
        DebounceSeconds = 3,
        ImageUrl = media?.Image,
        MapLink  = media?.MapLink,
        AudioUrl = media?.Audio,
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
    AppDb db, CancellationToken ct) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });

    var toLang = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang.Trim();
    var evt    = ParseEventTypeByte(eventType);

    var poiLang = await db.PoiLanguages.AsNoTracking()
        .FirstOrDefaultAsync(n => n.IdPoi == id && n.LanguageTag == toLang, ct);

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
// POST /api/v1/auth/register
// ============================================================
app.MapPost("/api/v1/auth/register", async (RegisterRequest req, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) ||
        string.IsNullOrWhiteSpace(req.Mail) ||
        string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Username, Mail và Password là bắt buộc" });
    if (await db.Users.AnyAsync(u => u.Username == req.Username))
        return Results.Conflict(new { error = "Username đã tồn tại" });
    if (await db.Users.AnyAsync(u => u.Mail == req.Mail))
        return Results.Conflict(new { error = "Email đã được đăng ký" });
    var user = new Users
    {
        UserId = Guid.NewGuid(),
        Username = req.Username.Trim(),
        Mail = req.Mail.Trim().ToLowerInvariant(),
        PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new { user.UserId, user.Username, user.Mail });
});

// ============================================================
// POST /api/v1/auth/login
// ============================================================
app.MapPost("/api/v1/auth/login", async (LoginRequest req, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "Username và Password là bắt buộc" });

    var user = await db.Users.FirstOrDefaultAsync(
        u => u.Username == req.Username && u.IsActive);

    if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        return Results.Unauthorized();

    var token = GenerateJwtToken(user, jwtKey);

    return Results.Ok(new
    {
        Token = token,
        UserId = user.UserId,
        user.Username,
        user.Mail,
        ExpiresAt = DateTime.UtcNow.AddHours(24)
    });
});

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
public sealed record RegisterRequest(string Username, string Mail, string Password);
public sealed record LoginRequest(string Username, string Password);
public sealed class HistoryRequest
{
    public int PoiId { get; set; }
    public Guid UserId { get; set; }
    public int? DurationSeconds { get; set; }
}
