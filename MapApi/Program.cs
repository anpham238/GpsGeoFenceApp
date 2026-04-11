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
        .Select(g => new { IdPoi = g.Key, g.First().Image, g.First().MapLink })
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
        ImageUrl = mediaMap.TryGetValue(p.Id, out var m) ? m.Image : null,
        MapLink = mediaMap.TryGetValue(p.Id, out var m2) ? m2.MapLink : null,
        Language = "vi-VN",
        NarrationText = (string?)null  // mobile sẽ fetch qua /narration endpoint
    });

    return Results.Ok(result);
});

// ============================================================
// GET /api/v1/pois/{id}
// ============================================================
app.MapGet("/api/v1/pois/{id}", async (string id, AppDb db) =>
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
        MapLink = media?.MapLink,
        Language = "vi-VN",
        NarrationText = (string?)null
    });
});

// ============================================================
// GET /api/v1/pois/{id}/narration?lang=...
// ============================================================
app.MapGet("/api/v1/pois/{id}/narration", async (
    string id, string? lang, string? eventType,
    AppDb db, TranslatorClient translator, CancellationToken ct) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    if (poi is null) return Results.NotFound(new { error = "POI not found" });
    var toLang = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang.Trim();
    var evt = ParseEventTypeByte(eventType);
    // 1) Tìm trong PoiLanguage
    var poiLang = await db.PoiLanguages.AsNoTracking()
        .FirstOrDefaultAsync(n => n.IdPoi == id && n.LanguageTag == toLang, ct);

    if (poiLang != null && !string.IsNullOrWhiteSpace(poiLang.NarTTS))
        return Results.Ok(new
        {
            PoiId = id,
            EventType = evt,
            Language = toLang,
            NarrationText = poiLang.NarTTS,
            Cached = true
        });
    // 2) Fallback: dịch từ tiếng Việt (dùng Name + Description thay vì NarrationText cũ)
    var baseText = $"Bạn đang đến gần {poi.Name}. {poi.Description ?? ""}".Trim();
    const string fromLang = "vi-VN";
    string finalText;
    if (string.Equals(fromLang, toLang, StringComparison.OrdinalIgnoreCase))
    {
        finalText = baseText;
    }
    else
    {
        var translated = await translator.TryTranslateAsync(baseText, toLang, fromLang, ct);
        finalText = string.IsNullOrWhiteSpace(translated) ? baseText : translated!;
    }

    // 3) Cache vào PoiLanguage để lần sau không cần dịch lại
    try
    {
        db.PoiLanguages.Add(new PoiLanguage
        {
            IdPoi = id,
            LanguageTag = toLang,
            NamePoi = poi.Name,
            NarTTS = finalText,
            Description = poi.Description
        });
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException) { /* unique constraint nếu đã có */ }

    return Results.Ok(new
    {
        PoiId = id,
        EventType = evt,
        Language = toLang,
        NarrationText = finalText,
        Cached = false
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
    if (string.IsNullOrWhiteSpace(req.PoiId) || req.UserId == Guid.Empty)
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
    public string PoiId { get; set; } = "";
    public Guid UserId { get; set; }
    public int? DurationSeconds { get; set; }
}
