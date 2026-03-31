using MapApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
// 1) Connection string: lấy từ appsettings.json, fallback local dev nếu thiếu
var cs = builder.Configuration.GetConnectionString("Default")
          ?? "Server=.\\SQLEXPRESS;Database=GpsApi;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

// 2) EF Core + SQL Server
builder.Services.AddDbContext<AppDb>(opt => opt.UseSqlServer(cs));

// 3) Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4) CORS (dev cho phép tất cả; prod nên giới hạn origin)
builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowAnyOrigin());
});

var app = builder.Build();

// 5) Middlewares
app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 6) (Dev) Tự áp dụng migrations khi khởi động
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    db.Database.Migrate(); // Khuyên dùng cho DEV. Prod nên apply script đã review.  [3](https://developer.android.com/about/versions/11/privacy/location)
}

// 7) Endpoint mẫu (POI) - nhóm v1
var v1 = app.MapGroup("/api/v1/pois");

// GET all
v1.MapGet("/", async (AppDb db) =>
{
    var items = await db.Pois.AsNoTracking()
        .OrderBy(p => p.Priority).ThenBy(p => p.Name)
        .ToListAsync();
    return Results.Ok(items);
});

// GET by id
v1.MapGet("/{id}", async (string id, AppDb db) =>
{
    var p = await db.Pois.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
    return p is null ? Results.NotFound() : Results.Ok(p);
});

// POST create
v1.MapPost("/", async (MapApi.Models.Poi p, AppDb db) =>
{
    if (string.IsNullOrWhiteSpace(p.Id)) p.Id = Guid.NewGuid().ToString();
    p.UpdatedAt = DateTime.UtcNow;
    db.Pois.Add(p);
    await db.SaveChangesAsync();
    return Results.Created($"/api/v1/pois/{p.Id}", p);
});

// PUT update
v1.MapPut("/{id}", async (string id, MapApi.Models.Poi input, AppDb db) =>
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
    e.NarrationText = input.NarrationText;
    e.AudioUrl = input.AudioUrl;
    e.MapLink = input.MapLink;
    e.IsActive = input.IsActive;
    e.UpdatedAt = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

// DELETE (mềm): set IsActive=false
v1.MapDelete("/{id}", async (string id, AppDb db) =>
{
    var e = await db.Pois.FirstOrDefaultAsync(x => x.Id == id);
    if (e is null) return Results.NotFound();
    e.IsActive = false;
    e.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

// Health ping
app.MapGet("/", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }));

app.Run();