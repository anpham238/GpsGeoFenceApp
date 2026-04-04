using MapApi.Data;
using MapApi.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── 1) Connection string SQL Server SSMS 2022 ─────────────────────────
// Mac dinh: Windows Authentication (Trusted_Connection)
// Neu dung SQL Authentication: "Server=.;Database=GpsApi;User Id=sa;Password=xxx;..."
var cs = builder.Configuration.GetConnectionString("Default")
          ?? "Server=.\\SQLEXPRESS;Database=GpsApi;Trusted_Connection=True;" +
             "Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

// ── 2) EF Core + SQL Server ───────────────────────────────────────────
builder.Services.AddDbContext<AppDb>(opt =>
    opt.UseSqlServer(cs, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    }));

// ── 3) Swagger ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "SmartTourism API", Version = "v1" }));

// ── 4) CORS ───────────────────────────────────────────────────────────
builder.Services.AddCors(opt =>
    opt.AddDefaultPolicy(p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin()));

var app = builder.Build();

// ── 5) Khoi tao DB khi start ─────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();

    // Kiem tra co ket noi duoc khong
    bool canConnect = false;
    try
    {
        canConnect = await db.Database.CanConnectAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[DB] Khong ket noi duoc SQL Server: {ex.Message}");
        Console.WriteLine("[DB] Kiem tra lai connection string trong appsettings.json");
    }

    if (canConnect)
    {
        try
        {
            // Neu da chay GpsApp.sql trong SSMS: bang da ton tai + __EFMigrationsHistory da co
            // Migrate() se kiem tra history va chi chay migration chua chay
            await db.Database.MigrateAsync();
            Console.WriteLine("[DB] Migration OK.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Migration warning: {ex.Message}");
            // Neu loi conflict (bang da ton tai tu SQL file):
            // Mo appsettings.json, doi UseSQL sang EnsureCreated (xem comment duoi)
        }
    }
}

// ── 6) Middleware ─────────────────────────────────────────────────────
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "SmartTourism v1"));
}

// ═══════════════════════════════════════════════════════════
// POI ENDPOINTS
// ═══════════════════════════════════════════════════════════
var pois = app.MapGroup("/api/v1/pois");

// GET /api/v1/pois — MAUI lay tat ca POI active
pois.MapGet("/", async (AppDb db) =>
{
    var items = await db.Pois
        .AsNoTracking()
        .Where(p => p.IsActive)
        .OrderBy(p => p.Priority)
        .ThenBy(p => p.Name)
        .ToListAsync();
    return Results.Ok(items);
}).WithName("GetAllPois").WithOpenApi();

// GET /api/v1/pois/sync?since=2025-01-01T00:00:00Z
// MAUI sync incremental (chi lay POI moi hon since)
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
}).WithName("SyncPois").WithOpenApi();

// GET /api/v1/pois/{id}
pois.MapGet("/{id}", async (string id, AppDb db) =>
{
    var p = await db.Pois.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id);
    return p is null ? Results.NotFound() : Results.Ok(p);
}).WithName("GetPoiById").WithOpenApi();

// POST /api/v1/pois — CMS tao moi
pois.MapPost("/", async (Poi p, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(p.Id))
        p.Id = Guid.NewGuid().ToString();
    p.UpdatedAt = DateTime.UtcNow;
    db.Pois.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/pois/{p.Id}", p);
}).WithName("CreatePoi").WithOpenApi();

// PUT /api/v1/pois/{id} — CMS cap nhat
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
}).WithName("UpdatePoi").WithOpenApi();

// DELETE /api/v1/pois/{id} — xoa mem
pois.MapDelete("/{id}", async (string id, AppDb db) =>
{
    var e = await db.Pois.FirstOrDefaultAsync(x => x.Id == id);
    if (e is null) return Results.NotFound();
    e.IsActive = false;
    e.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.NoContent();
}).WithName("DeletePoi").WithOpenApi();

// ═══════════════════════════════════════════════════════════
// PLAYBACK LOG ENDPOINTS
// ═══════════════════════════════════════════════════════════
var logs = app.MapGroup("/api/v1/playback");

// POST /api/v1/playback — MAUI gui log sau khi phat
logs.MapPost("/", async (PlaybackLog log, AppDb db) =>
{
    if (!await db.Pois.AnyAsync(p => p.Id == log.PoiId))
        return Results.BadRequest(new { error = "POI not found" });

    log.Id = 0;
    log.PlayedAt = DateTime.UtcNow;
    db.PlaybackLogs.Add(log);
    await db.SaveChangesAsync();
    return Results.Ok(new { log.Id, log.PlayedAt });
}).WithName("LogPlayback").WithOpenApi();

// GET /api/v1/playback/stats — thong ke cho CMS
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
}).WithName("PlaybackStats").WithOpenApi();

// Health check
app.MapGet("/", () => Results.Ok(new
{
    ok = true,
    time = DateTime.UtcNow,
    version = "SmartTourism API v1"
}));

app.Run();