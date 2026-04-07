using MapApi.Data;
using MapApi.Models;
using MapApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1) Connection string
var cs = builder.Configuration.GetConnectionString("Default")
         ?? "Server=.\\SQLEXPRESS;Database=GpsApi;Trusted_Connection=True;"
          + "Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

// 2) EF Core (✅ chỉ rõ AppDb)
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

// 5) Translator client (auto translate)
builder.Services.AddHttpClient<TranslatorClient>();

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Health
app.MapGet("/health", () => Results.Ok(new
{
    ok = true,
    time = DateTime.UtcNow,
    service = "GpsApi"
}));

// ============================================================
// GET /api/v1/pois  (KHÔNG lọc theo ngôn ngữ để vẽ map/pins)
// ============================================================
app.MapGet("/api/v1/pois", async (AppDb db) =>
{
    var items = await db.Pois
        .AsNoTracking()
        .Where(p => p.IsActive)
        .OrderBy(p => p.Priority)
        .ThenBy(p => p.Name)
        .ToListAsync();

    return Results.Ok(items);
});

// ============================================================
// GET /api/v1/pois/{id}
// ============================================================
app.MapGet("/api/v1/pois/{id}", async (string id, AppDb db) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
    return poi is null
        ? Results.NotFound(new { error = "POI not found" })
        : Results.Ok(poi);
});

// ============================================================
// GET /api/v1/pois/{id}/narration?lang=...&eventType=Enter|Near|Tap
// Auto translate + cache into dbo.PoiNarration
// Base text = dbo.Pois.NarrationText (vi) + fromLang = dbo.Pois.Language (vi-VN)
// ============================================================
app.MapGet("/api/v1/pois/{id}/narration", async (
    string id,
    string? lang,
    string? eventType,
    AppDb db,
    TranslatorClient translator,
    CancellationToken ct) =>
{
    var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    if (poi is null)
        return Results.NotFound(new { error = "POI not found" });

    var toLang = string.IsNullOrWhiteSpace(lang) ? "vi-VN" : lang.Trim();
    var evt = ParseNarrationEventType(eventType); // 0=Enter,1=Near,2=Tap

    // 1) Cache hit?
    var cached = await db.PoiNarrations.AsNoTracking()
        .Where(n => n.PoiId == id && n.EventType == evt && n.LanguageTag == toLang)
        .Select(n => n.NarrationText)
        .FirstOrDefaultAsync(ct);

    if (!string.IsNullOrWhiteSpace(cached))
    {
        return Results.Ok(new
        {
            PoiId = id,
            EventType = evt,
            Language = toLang,
            NarrationText = cached,
            Cached = true
        });
    }

    // 2) Base text from dbo.Pois.NarrationText (gốc)
    var baseText = poi.NarrationText;
    if (string.IsNullOrWhiteSpace(baseText))
        baseText = $"Bạn đang đến {poi.Name}. {poi.Description ?? ""}".Trim();

    // 3) Source language from dbo.Pois.Language (Option A), default vi-VN
    var fromLang = string.IsNullOrWhiteSpace(poi.Language) ? "vi-VN" : poi.Language!.Trim();

    // 4) Translate if needed (fallback to baseText if Translator missing config)
    string finalText;
    if (string.Equals(fromLang, toLang, StringComparison.OrdinalIgnoreCase))
    {
        finalText = baseText!;
    }
    else
    {
        var translated = await translator.TryTranslateAsync(baseText!, toLang, fromLang, ct);
        finalText = string.IsNullOrWhiteSpace(translated) ? baseText! : translated!;
    }

    // 5) Save cache to dbo.PoiNarration (ignore duplicates on race)
    try
    {
        db.PoiNarrations.Add(new PoiNarration
        {
            PoiId = id,
            EventType = evt,
            LanguageTag = toLang,
            NarrationText = finalText,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }
    catch (DbUpdateException)
    {
        // Unique key (PoiId,EventType,LanguageTag) may conflict if concurrent
    }

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
// POST /api/v1/playback
// Lưu đúng theo model PlaybackLog hiện tại (không có DurationSeconds/TriggerType)
// ============================================================
app.MapPost("/api/v1/playback", async (PlaybackCreateRequest req, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(req.PoiId))
        return Results.BadRequest(new { error = "PoiId is required" });

    if (!await db.Pois.AnyAsync(p => p.Id == req.PoiId))
        return Results.BadRequest(new { error = "POI not found" });

    var eventType = ParsePlaybackEventType(req.TriggerType);

    var log = new PlaybackLog
    {
        PoiId = req.PoiId,
        EventType = eventType,
        FiredAtUtc = DateTime.UtcNow,
        DeviceId = req.DeviceId,
        DistanceMeters = req.DistanceMeters
    };

    db.PoiPlaybackLog.Add(log);
    await db.SaveChangesAsync();

    return Results.Ok(new { log.Id, log.FiredAtUtc });
});

app.Run();

// ---------------- Helpers ----------------
static byte ParseNarrationEventType(string? s)
{
    return (s ?? "").Trim().ToLowerInvariant() switch
    {
        "enter" => 0,
        "near" => 1,
        "tap" => 2,
        _ => 0
    };
}

static byte ParsePlaybackEventType(string? triggerType)
{
    // 0=Enter,1=Near,2=Tap,3=QR (QR chỉ dùng cho log)
    return (triggerType ?? "").Trim().ToUpperInvariant() switch
    {
        "ENTER" => 0,
        "NEAR" => 1,
        "TAP" => 2,
        "QR" => 3,
        _ => 0
    };
}

// ---------------- Request DTO ----------------
public sealed class PlaybackCreateRequest
{
    public string PoiId { get; set; } = "";
    public string? TriggerType { get; set; }       // ENTER/NEAR/TAP/QR
    public int? DurationSeconds { get; set; }      // hiện chưa lưu vì PlaybackLog model chưa có field này
    public int? DistanceMeters { get; set; }
    public string? DeviceId { get; set; }
    public bool? IsSuccess { get; set; }           // hiện chưa lưu vì PlaybackLog model chưa có field này
}