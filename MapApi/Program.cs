using MapApi.Data;
using MapApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1) Connection string
var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Server=.\\SQLEXPRESS;Database=GpsApi;Trusted_Connection=True;"
          + "Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

// 2) EF Core
builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseSqlServer(cs, sql =>
    {
        sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
        sql.CommandTimeout(120);
    }));

// 3) Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4) CORS
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));
var app = builder.Build();
app.UseCors();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health (đổi sang /health để tránh trùng route "/")
app.MapGet("/health", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }));

// =======================
// POI endpoints
// =======================
var pois = app.MapGroup("/api/v1/pois");

pois.MapGet("/", async (AppDb db) =>
{
    var items = await db.Pois
        .AsNoTracking()
        .Where(p => p.IsActive)
        .OrderBy(p => p.Priority)
        .ThenBy(p => p.Name)
        .ToListAsync();

    return Results.Ok(items);
});

pois.MapGet("/sync", async (DateTime? since, AppDb db) =>
{
    var q = db.Pois.AsNoTracking().AsQueryable();
    if (since.HasValue)
        q = q.Where(p => p.UpdatedAt > since.Value);

    var items = await q
        .OrderBy(p => p.Priority)
        .ThenBy(p => p.Name)
        .ToListAsync();

    return Results.Ok(new { Items = items, ServerTime = DateTime.UtcNow });
});

pois.MapGet("/{id}", async (string id, AppDb db) =>
{
    var p = await db.Pois.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    return p is null ? Results.NotFound() : Results.Ok(p);
});

pois.MapPost("/", async (Poi p, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(p.Id))
        p.Id = Guid.NewGuid().ToString();

    p.UpdatedAt = DateTime.UtcNow;
    db.Pois.Add(p);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/pois/{p.Id}", p);
});

pois.MapPut("/{id}", async (string id, Poi input, AppDb db) =>
{
    var e = await db.Pois.FirstOrDefaultAsync(x => x.Id == id);
    if (e is null) return Results.NotFound();

    e.Name = input.Name;
    e.Description = input.Description;
    e.Latitude = input.Latitude;
    e.Longitude = input.Longitude;
    e.RadiusMeters = input.RadiusMeters;
    e.NearRadiusMeters = input.NearRadiusMeters;
    e.DebounceSeconds = input.DebounceSeconds;
    e.CooldownSeconds = input.CooldownSeconds;
    e.Priority = input.Priority;
    e.Language = input.Language;
    e.NarrationText = input.NarrationText;
    e.AudioUrl = input.AudioUrl;
    e.ImageUrl = input.ImageUrl;
    e.MapLink = input.MapLink;
    e.IsActive = input.IsActive;

    e.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(e);
});

pois.MapDelete("/{id}", async (string id, AppDb db) =>
{
    var e = await db.Pois.FirstOrDefaultAsync(x => x.Id == id);
    if (e is null) return Results.NotFound();

    e.IsActive = false;
    e.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.NoContent();
});

// =======================
// Playback endpoints
// =======================
var logs = app.MapGroup("/api/v1/playback");

logs.MapPost("/", async (PlaybackLog log, AppDb db) =>
{
    if (!await db.Pois.AnyAsync(p => p.Id == log.PoiId))
        return Results.BadRequest(new { error = "POI not found" });

    log.Id = 0;
    log.PlayedAt = DateTime.UtcNow;
    db.PlaybackLogs.Add(log);
    await db.SaveChangesAsync();

    return Results.Ok(new { log.Id, log.PlayedAt });
});

logs.MapGet("/stats", async (AppDb db) =>
{
    var stats = await db.PlaybackLogs
        .GroupBy(l => l.PoiId)
        .Select(g => new
        {
            PoiId = g.Key,
            PlayCount = g.Count(),
            AvgSeconds = g.Average(x => (double?)x.DurationListened ?? 0),
            LastPlayed = g.Max(x => x.PlayedAt)
        })
        .OrderByDescending(x => x.PlayCount)
        .Take(20)
        .ToListAsync();

    return Results.Ok(stats);
});
// GET /api/v1/pois/{id}/narration?lang=vi-VN&eventType=Enter
pois.MapGet("/{id}/narration", async (string id, string? lang, string? eventType, AppDb db) =>
{
    // 1) validate
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    if (poi is null) return Results.NotFound();

    var language = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang.Trim();
    var evt = ParseEventType(eventType); // byte

    // 2) exact match (vi-VN)
    var item = await db.PoiNarrations.AsNoTracking()
        .Where(x => x.PoiId == id && x.EventType == evt && x.LanguageTag == language)
        .Select(x => x.NarrationText)
        .FirstOrDefaultAsync();

    // 3) fallback by primary language (vi)
    if (string.IsNullOrWhiteSpace(item) && language.Contains('-'))
    {
        var primary = language.Split('-')[0];
        item = await db.PoiNarrations.AsNoTracking()
            .Where(x => x.PoiId == id && x.EventType == evt && x.LanguageTag.StartsWith(primary))
            .OrderBy(x => x.LanguageTag) // chọn ổn định
            .Select(x => x.NarrationText)
            .FirstOrDefaultAsync();
    }

    // 4) fallback to vi-VN
    if (string.IsNullOrWhiteSpace(item) && !string.Equals(language, "vi-VN", StringComparison.OrdinalIgnoreCase))
    {
        item = await db.PoiNarrations.AsNoTracking()
            .Where(x => x.PoiId == id && x.EventType == evt && x.LanguageTag == "vi-VN")
            .Select(x => x.NarrationText)
            .FirstOrDefaultAsync();
    }

    // 5) fallback to Pois.NarrationText (bản chung) [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
    item ??= poi.NarrationText;

    return Results.Ok(new
    {
        PoiId = id,
        EventType = evt,
        Language = language,
        NarrationText = item
    });
});
static byte ParseEventType(string? s)
{
    // Mình chốt mapping: Enter=0, Near=1, Tap=2 (phải thống nhất với dữ liệu trong PoiNarration) [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/GpsApp.sql)
    return (s ?? "").Trim().ToLowerInvariant() switch
    {
        "enter" => 0,
        "near" => 1,
        "tap" => 2,
        _ => 0
    };
}
app.Run();
