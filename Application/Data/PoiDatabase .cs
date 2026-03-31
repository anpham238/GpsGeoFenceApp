using Microsoft.Data.Sqlite;
using MauiApp1.Models;
using System.Diagnostics;

namespace MauiApp1.Data;

/// <summary>
/// Quan ly POI bang SQLite local.
/// File DB: smarttourism.db3 trong FileSystem.AppDataDirectory
/// Tu dong tao bang + seed du lieu lan dau.
/// </summary>
public class PoiDatabase
{
    private bool _initialized;
    private readonly SemaphoreSlim _lock = new(1, 1);
    public async Task InitAsync()
    {
        if (_initialized) return;
        await _lock.WaitAsync();
        try
        {
            if (_initialized) return;

            await using var conn = new SqliteConnection(Constants.ConnectionString);
            await conn.OpenAsync();

            // Tao bang POIs
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS POIs (
                    Id               TEXT    PRIMARY KEY NOT NULL,
                    Name             TEXT    NOT NULL,
                    Description      TEXT    NOT NULL DEFAULT '',
                    Latitude         REAL    NOT NULL,
                    Longitude        REAL    NOT NULL,
                    RadiusMeters     REAL    NOT NULL DEFAULT 120,
                    NearRadiusMeters REAL    NOT NULL DEFAULT 220,
                    DebounceSeconds  INTEGER NOT NULL DEFAULT 3,
                    CooldownSeconds  INTEGER NOT NULL DEFAULT 30,
                    NarrationText    TEXT,
                    AudioUrl         TEXT,
                    ImageUrl         TEXT,
                    MapLink          TEXT,
                    IsActive         INTEGER NOT NULL DEFAULT 1,
                    Priority         INTEGER NOT NULL DEFAULT 1,
                    CreatedAt        TEXT    NOT NULL DEFAULT (datetime('now','utc')),
                    UpdatedAt        TEXT    NOT NULL DEFAULT (datetime('now','utc'))
                );
                CREATE INDEX IF NOT EXISTS idx_poi_active
                    ON POIs (IsActive, Priority ASC);";
            await cmd.ExecuteNonQueryAsync();

            // Seed neu bang trong
            await SeedIfEmptyAsync(conn);

            _initialized = true;
            Debug.WriteLine($"[SQLite] Ready: {Constants.DatabasePath}");
        }
        finally { _lock.Release(); }
    }
    private async Task SeedIfEmptyAsync(SqliteConnection conn)
    {
        var countCmd = conn.CreateCommand();
        countCmd.CommandText = "SELECT COUNT(*) FROM POIs";
        var count = (long)(await countCmd.ExecuteScalarAsync() ?? 0L);
        if (count > 0) return;

        var pois = new[]
        {
            ("poi-hcm-001",       "Trung tam TP.HCM",            10.776889, 106.700806, 150f, 300f,
             "Chao mung den Thanh pho Ho Chi Minh, trai tim kinh te cua Viet Nam.",
             "https://maps.google.com/?q=10.776889,106.700806", 1),

            ("poi-benthanh-001",  "Cho Ben Thanh",               10.772450, 106.698060, 100f, 200f,
             "Ban dang den Cho Ben Thanh, bieu tuong lich su tren 100 nam cua Sai Gon.",
             "https://maps.google.com/?q=10.77245,106.69806", 2),

            ("poi-notredame-001", "Nha tho Duc Ba",              10.779930, 106.699330,  80f, 160f,
             "Truoc mat ban la Nha tho Duc Ba Sai Gon, cong trinh Gothic xay dung tu 1863.",
             "https://maps.google.com/?q=10.77993,106.69933", 3),

            ("poi-postoffice-001","Buu dien Trung tam Sai Gon",  10.779760, 106.699600,  80f, 160f,
             "Day la Buu dien Trung tam Sai Gon, biet thu Phap dep xay nam 1886.",
             "https://maps.google.com/?q=10.77976,106.6996", 4),

            ("poi-park304-001",   "Cong vien 30-4",              10.777600, 106.695400, 100f, 200f,
             "Ban dang o Cong vien 30-4, nhin ve Dinh Doc Lap phia sau ban.",
             "https://maps.google.com/?q=10.7776,106.6954", 5),

            ("poi-reunif-001",    "Dinh Doc Lap",                10.776900, 106.695400, 100f, 200f,
             "Dinh Doc Lap - noi ghi dau nhieu su kien lich su cua Viet Nam.",
             "https://maps.google.com/?q=10.7769,106.6954", 6),

            ("poi-ntmk-001",      "Cong vien NTMK",              10.787000, 106.700000, 120f, 240f,
             "Cong vien Nguyen Thi Minh Khai, diem xanh yeu binh giua long thanh pho.",
             "https://maps.google.com/?q=10.787,106.700", 7),
        };

        foreach (var (id, name, lat, lng, radius, near, tts, maplink, pri) in pois)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT OR IGNORE INTO POIs
                    (Id,Name,Description,Latitude,Longitude,
                     RadiusMeters,NearRadiusMeters,
                     NarrationText,MapLink,IsActive,Priority)
                VALUES
                    ($id,$name,$name,$lat,$lng,
                     $r,$n,
                     $tts,$map,1,$pri)";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$lat", lat);
            cmd.Parameters.AddWithValue("$lng", lng);
            cmd.Parameters.AddWithValue("$r", radius);
            cmd.Parameters.AddWithValue("$n", near);
            cmd.Parameters.AddWithValue("$tts", tts);
            cmd.Parameters.AddWithValue("$map", maplink);
            cmd.Parameters.AddWithValue("$pri", pri);
            await cmd.ExecuteNonQueryAsync();
        }
        Debug.WriteLine($"[SQLite] Seed {pois.Length} POI xong.");
    }
    public async Task<List<Poi>> GetActivePoisAsync()
    {
        await InitAsync();
        var result = new List<Poi>();
        try
        {
            await using var conn = new SqliteConnection(Constants.ConnectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText =
                "SELECT * FROM POIs WHERE IsActive=1 ORDER BY Priority ASC, Name ASC";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) result.Add(Read(r));
            Debug.WriteLine($"[SQLite] Lay {result.Count} POI");
        }
        catch (Exception ex) { Debug.WriteLine($"[SQLite] Get loi: {ex.Message}"); }
        return result;
    }

    public async Task<Poi?> GetByIdAsync(string id)
    {
        await InitAsync();
        try
        {
            await using var conn = new SqliteConnection(Constants.ConnectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM POIs WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? Read(r) : null;
        }
        catch (Exception ex) { Debug.WriteLine($"[SQLite] GetById loi: {ex.Message}"); }
        return null;
    }
    public async Task<bool> SaveAsync(Poi p)
    {
        await InitAsync();
        try
        {
            await using var conn = new SqliteConnection(Constants.ConnectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO POIs
                    (Id,Name,Description,Latitude,Longitude,
                     RadiusMeters,NearRadiusMeters,DebounceSeconds,CooldownSeconds,
                     NarrationText,AudioUrl,ImageUrl,MapLink,
                     IsActive,Priority,CreatedAt,UpdatedAt)
                VALUES
                    ($id,$name,$desc,$lat,$lng,
                     $r,$nr,$db,$cd,
                     $tts,$audio,$img,$map,
                     $active,$pri,$created,$updated)
                ON CONFLICT(Id) DO UPDATE SET
                    Name=$name, Description=$desc,
                    Latitude=$lat, Longitude=$lng,
                    RadiusMeters=$r, NearRadiusMeters=$nr,
                    DebounceSeconds=$db, CooldownSeconds=$cd,
                    NarrationText=$tts, AudioUrl=$audio,
                    ImageUrl=$img, MapLink=$map,
                    IsActive=$active, Priority=$pri,
                    UpdatedAt=$updated";
            Bind(cmd, p);
            await cmd.ExecuteNonQueryAsync();
            Debug.WriteLine($"[SQLite] Save OK: {p.Name}");
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"[SQLite] Save loi: {ex.Message}"); return false; }
    }
    public async Task<bool> DeleteAsync(string id)
    {
        await InitAsync();
        try
        {
            await using var conn = new SqliteConnection(Constants.ConnectionString);
            await conn.OpenAsync();
            var cmd = conn.CreateCommand();
            cmd.CommandText =
                "UPDATE POIs SET IsActive=0, UpdatedAt=$now WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch (Exception ex) { Debug.WriteLine($"[SQLite] Delete loi: {ex.Message}"); return false; }
    }
    private static Poi Read(SqliteDataReader r)
    {
        string? S(string col) { int i = r.GetOrdinal(col); return r.IsDBNull(i) ? null : r.GetString(i); }
        return new Poi
        {
            Id = r.GetString(r.GetOrdinal("Id")),
            Name = r.GetString(r.GetOrdinal("Name")),
            Description = S("Description") ?? "",
            Latitude = r.GetDouble(r.GetOrdinal("Latitude")),
            Longitude = r.GetDouble(r.GetOrdinal("Longitude")),
            RadiusMeters = (float)r.GetDouble(r.GetOrdinal("RadiusMeters")),
            NearRadiusMeters = (float)r.GetDouble(r.GetOrdinal("NearRadiusMeters")),
            DebounceSeconds = r.GetInt32(r.GetOrdinal("DebounceSeconds")),
            CooldownSeconds = r.GetInt32(r.GetOrdinal("CooldownSeconds")),
            NarrationText = S("NarrationText"),
            AudioUrl = S("AudioUrl"),
            ImageUrl = S("ImageUrl"),
            MapLink = S("MapLink"),
            IsActive = r.GetInt32(r.GetOrdinal("IsActive")) == 1,
            Priority = r.GetInt32(r.GetOrdinal("Priority")),
        };
    }
   private static void Bind(SqliteCommand cmd, Poi p)
    {
        var now = DateTime.UtcNow.ToString("o");
        cmd.Parameters.AddWithValue("$id", p.Id);
        cmd.Parameters.AddWithValue("$name", p.Name);
        cmd.Parameters.AddWithValue("$desc", p.Description);
        cmd.Parameters.AddWithValue("$lat", p.Latitude);
        cmd.Parameters.AddWithValue("$lng", p.Longitude);
        cmd.Parameters.AddWithValue("$r", p.RadiusMeters);
        cmd.Parameters.AddWithValue("$nr", p.NearRadiusMeters);
        cmd.Parameters.AddWithValue("$db", p.DebounceSeconds);
        cmd.Parameters.AddWithValue("$cd", p.CooldownSeconds);
        cmd.Parameters.AddWithValue("$tts", (object?)p.NarrationText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$audio", (object?)p.AudioUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$img", (object?)p.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$map", (object?)p.MapLink ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$active", p.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$pri", p.Priority);
        cmd.Parameters.AddWithValue("$created", p.CreatedAt == default ? now : p.CreatedAt.ToString("o"));
        cmd.Parameters.AddWithValue("$updated", now);
    }
}