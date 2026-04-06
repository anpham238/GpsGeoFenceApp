using System.Text;
using System.Text.RegularExpressions;
using MapApi.Data;
using MapApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── 1) Connection string ─────────────────────────────────────────────
var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Server=.\\SQLEXPRESS;Database=GpsApi;Trusted_Connection=True;"
          + "Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

// ── 2) EF Core + SQL Server ──────────────────────────────────────────
builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseSqlServer(cs, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
        // Tuỳ chọn: tăng timeout nếu máy yếu / cold start
        sql.CommandTimeout(120);
    }));

// ── 3) Swagger ───────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "SmartTourism API", Version = "v1" }));

// ── 4) CORS ──────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin()));

var app = builder.Build();

// ── 5) DB init/migrate (dev) ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    try
    {
        if (await db.Database.CanConnectAsync())
        {
            try
            {
                await db.Database.MigrateAsync();
                Console.WriteLine("[DB] Migration OK.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DB] Migration warning: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("[DB] Cannot connect to SQL Server.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] Connection error: {ex.Message}");
    }
}

// ── 6) Middleware ────────────────────────────────────────────────────
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartTourism v1"));
}

// ═══════════════════════════════════════════════════════════
// POI ENDPOINTS
// ═══════════════════════════════════════════════════════════
var pois = app.MapGroup("/api/v1/pois").WithTags("POIs");

// GET /api/v1/pois
pois.MapGet("/", async (AppDb db) =>
{
    var items = await db.Pois
        .AsNoTracking()
        .Where(p => p.IsActive)
        .OrderBy(p => p.Priority)
        .ThenBy(p => p.Name)
        .ToListAsync();

    return Results.Ok(items);
})
.WithName("GetAllPois");

// GET /api/v1/pois/sync?since=...
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
})
.WithName("SyncPois");

// GET /api/v1/pois/{id}
pois.MapGet("/{id}", async (string id, AppDb db) =>
{
    var p = await db.Pois.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    return p is null ? Results.NotFound() : Results.Ok(p);
})
.WithName("GetPoiById");

// POST /api/v1/pois
pois.MapPost("/", async (Poi p, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(p.Id))
        p.Id = Guid.NewGuid().ToString();

    p.UpdatedAt = DateTime.UtcNow;
    db.Pois.Add(p);
    await db.SaveChangesAsync();

    return Results.Created($"/api/v1/pois/{p.Id}", p);
})
.WithName("CreatePoi");

// PUT /api/v1/pois/{id}
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

    // ❌ BỎ Language: DB dbo.Pois không có cột Language theo script của bạn
    // e.Language = input.Language;

    e.NarrationText = input.NarrationText;
    e.AudioUrl = input.AudioUrl;
    e.ImageUrl = input.ImageUrl;
    e.MapLink = input.MapLink;
    e.IsActive = input.IsActive;

    e.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(e);
})
.WithName("UpdatePoi");

// DELETE /api/v1/pois/{id} — soft delete
pois.MapDelete("/{id}", async (string id, AppDb db) =>
{
    var e = await db.Pois.FirstOrDefaultAsync(x => x.Id == id);
    if (e is null) return Results.NotFound();

    e.IsActive = false;
    e.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.NoContent();
})
.WithName("DeletePoi");

// ═══════════════════════════════════════════════════════════
// PLAYBACK LOG ENDPOINTS
// ═══════════════════════════════════════════════════════════
var logs = app.MapGroup("/api/v1/playback").WithTags("Playback");

// POST /api/v1/playback
logs.MapPost("/", async (PlaybackLog log, AppDb db) =>
{
    if (!await db.Pois.AnyAsync(p => p.Id == log.PoiId))
        return Results.BadRequest(new { error = "POI not found" });

    log.Id = 0;
    log.PlayedAt = DateTime.UtcNow;

    db.PlaybackLogs.Add(log);
    await db.SaveChangesAsync();

    return Results.Ok(new { log.Id, log.PlayedAt });
})
.WithName("LogPlayback");

// GET /api/v1/playback/stats
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
})
.WithName("PlaybackStats");

// GET /api/v1/pois/{id}/qr
pois.MapGet("/{id}/qr", async (string id, AppDb db) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    if (poi is null) return Results.NotFound();

    var content = $"smarttourism://poi/{id}";

    using var qrGenerator = new QRCoder.QRCodeGenerator();
    var qrData = qrGenerator.CreateQrCode(content, QRCoder.QRCodeGenerator.ECCLevel.Q);
    using var qrCode = new QRCoder.PngByteQRCode(qrData);
    var pngBytes = qrCode.GetGraphic(6);

    // sanitize filename (tránh ký tự lạ)
    var safeName = SanitizeFileName(poi.Name);
    return Results.File(pngBytes, "image/png", fileDownloadName: $"qr_{safeName}.png");
})
.WithName("GetPoiQr");

// Health check
app.MapGet("/", () => Results.Ok(new
{
    ok = true,
    time = DateTime.UtcNow,
    version = "SmartTourism API v1"
}))
.WithTags("Health");

app.Run();

// Helpers
static string SanitizeFileName(string? name)
{
    name ??= "poi";
    var invalid = Path.GetInvalidFileNameChars();
    var sb = new StringBuilder(name.Length);
    foreach (var ch in name)
        sb.Append(invalid.Contains(ch) ? '_' : ch);

    // tránh tên rỗng
    var s = sb.ToString().Trim();
    return string.IsNullOrWhiteSpace(s) ? "poi" : s;
}