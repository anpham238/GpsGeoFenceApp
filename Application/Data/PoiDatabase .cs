using Microsoft.Data.Sqlite;
using MauiApp1.Models;

namespace MauiApp1.Data;

public sealed class PoiDatabase
{
    private bool _inited;
    private const string TableName = "Pois";

    public async Task InitAsync()
    {
        if (_inited) return;

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {TableName}(
  Id TEXT PRIMARY KEY,
  Name TEXT NOT NULL,
  Description TEXT NOT NULL,
  Latitude REAL NOT NULL,
  Longitude REAL NOT NULL,
  RadiusMeters INTEGER NOT NULL,
  NearRadiusMeters INTEGER NOT NULL,
  DebounceSeconds INTEGER NOT NULL,
  CooldownSeconds INTEGER NOT NULL,
  Priority INTEGER NULL,
  NarrationText TEXT NULL,
  AudioUrl TEXT NULL,
  ImageUrl TEXT NULL,
  MapLink TEXT NULL,
  IsActive INTEGER NOT NULL,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_{TableName}_ActivePriority ON {TableName}(IsActive, Priority, Name);
";
        await cmd.ExecuteNonQueryAsync();

        _inited = true;
        System.Diagnostics.Debug.WriteLine($"[SQLite] Ready: {Constants.DatabasePath}"); // chỉ log đường dẫn [1](https://svsguedu-my.sharepoint.com/personal/3123411204_sv_sgu_edu_vn/Documents/Microsoft%20Copilot%20Chat%20Files/Constants.cs)
    }

    public async Task<List<Poi>> GetActivePoisAsync()
    {
        await InitAsync();
        var list = new List<Poi>();

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
SELECT Id, Name, Description, Latitude, Longitude,
       RadiusMeters, NearRadiusMeters, DebounceSeconds, CooldownSeconds,
       Priority, NarrationText, AudioUrl, ImageUrl, MapLink,
       IsActive, CreatedAt, UpdatedAt
FROM {TableName}
WHERE IsActive = 1
ORDER BY Priority, Name;
";

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Poi
            {
                Id = r.GetString(0),
                Name = r.GetString(1),
                Description = r.GetString(2),
                Latitude = r.GetDouble(3),
                Longitude = r.GetDouble(4),
                RadiusMeters = r.GetInt32(5),
                NearRadiusMeters = r.GetInt32(6),
                DebounceSeconds = r.GetInt32(7),
                CooldownSeconds = r.GetInt32(8),
                Priority = r.IsDBNull(9) ? null : r.GetInt32(9),
                NarrationText = r.IsDBNull(10) ? null : r.GetString(10),
                AudioUrl = r.IsDBNull(11) ? null : r.GetString(11),
                ImageUrl = r.IsDBNull(12) ? null : r.GetString(12),
                MapLink = r.IsDBNull(13) ? null : r.GetString(13),
                IsActive = r.GetInt32(14) == 1,
                CreatedAt = DateTime.Parse(r.GetString(15)),
                UpdatedAt = DateTime.Parse(r.GetString(16)),
            });
        }

        System.Diagnostics.Debug.WriteLine($"[SQLite] GetActivePoisAsync -> {list.Count}");
        return list;
    }

    // ✅ Upsert theo Id để sync server -> local không bị trùng
    public async Task SaveAsync(Poi p)
    {
        await InitAsync();

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
INSERT INTO {TableName}(
  Id, Name, Description, Latitude, Longitude,
  RadiusMeters, NearRadiusMeters, DebounceSeconds, CooldownSeconds,
  Priority, NarrationText, AudioUrl, ImageUrl, MapLink,
  IsActive, CreatedAt, UpdatedAt
) VALUES (
  $Id, $Name, $Description, $Latitude, $Longitude,
  $RadiusMeters, $NearRadiusMeters, $DebounceSeconds, $CooldownSeconds,
  $Priority, $NarrationText, $AudioUrl, $ImageUrl, $MapLink,
  $IsActive, $CreatedAt, $UpdatedAt
)
ON CONFLICT(Id) DO UPDATE SET
  Name=excluded.Name,
  Description=excluded.Description,
  Latitude=excluded.Latitude,
  Longitude=excluded.Longitude,
  RadiusMeters=excluded.RadiusMeters,
  NearRadiusMeters=excluded.NearRadiusMeters,
  DebounceSeconds=excluded.DebounceSeconds,
  CooldownSeconds=excluded.CooldownSeconds,
  Priority=excluded.Priority,
  NarrationText=excluded.NarrationText,
  AudioUrl=excluded.AudioUrl,
  ImageUrl=excluded.ImageUrl,
  MapLink=excluded.MapLink,
  IsActive=excluded.IsActive,
  UpdatedAt=excluded.UpdatedAt;
";

        cmd.Parameters.AddWithValue("$Id", p.Id);
        cmd.Parameters.AddWithValue("$Name", p.Name);
        cmd.Parameters.AddWithValue("$Description", p.Description ?? "");
        cmd.Parameters.AddWithValue("$Latitude", p.Latitude);
        cmd.Parameters.AddWithValue("$Longitude", p.Longitude);
        cmd.Parameters.AddWithValue("$RadiusMeters", p.RadiusMeters);
        cmd.Parameters.AddWithValue("$NearRadiusMeters", p.NearRadiusMeters);
        cmd.Parameters.AddWithValue("$DebounceSeconds", p.DebounceSeconds);
        cmd.Parameters.AddWithValue("$CooldownSeconds", p.CooldownSeconds);
        cmd.Parameters.AddWithValue("$Priority", (object?)p.Priority ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$NarrationText", (object?)p.NarrationText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$AudioUrl", (object?)p.AudioUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ImageUrl", (object?)p.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$MapLink", (object?)p.MapLink ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$IsActive", p.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$CreatedAt", (p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt).ToString("O"));
        cmd.Parameters.AddWithValue("$UpdatedAt", (p.UpdatedAt == default ? DateTime.UtcNow : p.UpdatedAt).ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }
}