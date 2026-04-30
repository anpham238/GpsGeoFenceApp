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

        // Tự động dọn dẹp bảng cũ nếu có rác
        try
        {
            var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = $"PRAGMA table_info({TableName});";
            await using var infoReader = await checkCmd.ExecuteReaderAsync();
            bool needsDrop = false;
            while (await infoReader.ReadAsync())
            {
                var colName = infoReader.GetString(1);
                var colType = infoReader.GetString(2);
                if ((colName.Equals("Id", StringComparison.OrdinalIgnoreCase) && colType.Equals("TEXT", StringComparison.OrdinalIgnoreCase)) ||
                    colName.Equals("NearRadiusMeters", StringComparison.OrdinalIgnoreCase))
                {
                    needsDrop = true;
                    break;
                }
            }
            await infoReader.CloseAsync();

            if (needsDrop)
            {
                var dropCmd = conn.CreateCommand();
                dropCmd.CommandText = $"DROP TABLE IF EXISTS {TableName};";
                await dropCmd.ExecuteNonQueryAsync();
            }
        }
        catch { }

        var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
CREATE TABLE IF NOT EXISTS {TableName}(
  Id INTEGER PRIMARY KEY,
  Name TEXT NOT NULL,
  Description TEXT,
  Latitude REAL NOT NULL,
  Longitude REAL NOT NULL,
  RadiusMeters INTEGER NOT NULL,
  CooldownSeconds INTEGER NOT NULL,
  NarrationText TEXT NULL,
  AudioUrl TEXT NULL,
  ImageUrl TEXT NULL,
  MapLink TEXT NULL,
  IsActive INTEGER NOT NULL,
  CreatedAt TEXT NOT NULL,
  UpdatedAt TEXT NOT NULL,
  Language TEXT NULL DEFAULT 'vi-VN'
);

CREATE INDEX IF NOT EXISTS IX_{TableName}_Active ON {TableName}(IsActive, Name);
";
        await cmd.ExecuteNonQueryAsync();

        _inited = true;
        System.Diagnostics.Debug.WriteLine($"[SQLite] Ready: {Constants.DatabasePath}");
    }

    public async Task SaveAsync(Poi p)
    {
        await InitAsync();

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Pois(
  Id, Name, Description, Latitude, Longitude,
  RadiusMeters, CooldownSeconds,
  NarrationText, AudioUrl, ImageUrl, MapLink,
  IsActive, CreatedAt, UpdatedAt, Language
) VALUES (
  $Id, $Name, $Description, $Latitude, $Longitude,
  $RadiusMeters, $CooldownSeconds, 
  $NarrationText, $AudioUrl, $ImageUrl, $MapLink,
  $IsActive, $CreatedAt, $UpdatedAt, $Language
)
ON CONFLICT(Id) DO UPDATE SET
  Name=excluded.Name,
  Description=excluded.Description,
  Latitude=excluded.Latitude,
  Longitude=excluded.Longitude,
  RadiusMeters=excluded.RadiusMeters,
  CooldownSeconds=excluded.CooldownSeconds,
  NarrationText=excluded.NarrationText,
  AudioUrl=excluded.AudioUrl,
  ImageUrl=excluded.ImageUrl,
  MapLink=excluded.MapLink,
  IsActive=excluded.IsActive,
  UpdatedAt=excluded.UpdatedAt,
  Language=excluded.Language;
";
        cmd.Parameters.AddWithValue("$Id", p.Id);
        cmd.Parameters.AddWithValue("$Name", p.Name);
        cmd.Parameters.AddWithValue("$Description", p.Description ?? "");
        cmd.Parameters.AddWithValue("$Latitude", p.Latitude);
        cmd.Parameters.AddWithValue("$Longitude", p.Longitude);
        cmd.Parameters.AddWithValue("$RadiusMeters", p.RadiusMeters);
        cmd.Parameters.AddWithValue("$CooldownSeconds", p.CooldownSeconds);
        cmd.Parameters.AddWithValue("$NarrationText", (object?)p.NarrationText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$AudioUrl", (object?)p.AudioUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ImageUrl", (object?)p.ImageUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$MapLink", (object?)p.MapLink ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$IsActive", p.IsActive ? 1 : 0);
        var created = p.CreatedAt == default ? DateTime.UtcNow : p.CreatedAt;
        var updated = p.UpdatedAt == default ? DateTime.UtcNow : p.UpdatedAt;
        cmd.Parameters.AddWithValue("$CreatedAt", created.ToString("O"));
        cmd.Parameters.AddWithValue("$UpdatedAt", updated.ToString("O"));
        cmd.Parameters.AddWithValue("$Language", (object?)p.Language ?? "vi-VN");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Poi>> GetActivePoisAsync()
    {
        await InitAsync();

        var list = new List<Poi>();
        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
SELECT
  Id, Name, Description, Latitude, Longitude,
  RadiusMeters, CooldownSeconds,
  NarrationText, AudioUrl, ImageUrl, MapLink,
  IsActive, CreatedAt, UpdatedAt, Language
FROM {TableName}
WHERE IsActive = 1
ORDER BY Name;
";

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Poi
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Description = r.IsDBNull(2) ? null : r.GetString(2),
                Latitude = r.GetDouble(3),
                Longitude = r.GetDouble(4),
                RadiusMeters = r.GetInt32(5),
                CooldownSeconds = r.GetInt32(6),
                NarrationText = r.IsDBNull(7) ? null : r.GetString(7),
                AudioUrl = r.IsDBNull(8) ? null : r.GetString(8),
                ImageUrl = r.IsDBNull(9) ? null : r.GetString(9),
                MapLink = r.IsDBNull(10) ? null : r.GetString(10),
                IsActive = r.GetInt32(11) == 1,
                CreatedAt = DateTime.Parse(r.GetString(12)),
                UpdatedAt = DateTime.Parse(r.GetString(13)),
                Language = r.IsDBNull(14) ? "vi-VN" : r.GetString(14),
            });
        }

        System.Diagnostics.Debug.WriteLine($"[SQLite] GetActivePoisAsync -> {list.Count}");
        return list;
    }

    public async Task<HashSet<int>> GetAllIdsAsync()
    {
        await InitAsync();

        var ids = new HashSet<int>();
        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();
        var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id FROM {TableName};";
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            ids.Add(r.GetInt32(0));
        return ids;
    }

    public async Task<Poi?> GetByIdAsync(int id)
    {
        await InitAsync();

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
SELECT
  Id, Name, Description, Latitude, Longitude,
  RadiusMeters, CooldownSeconds,
  NarrationText, AudioUrl, ImageUrl, MapLink,
  IsActive, CreatedAt, UpdatedAt, Language
FROM {TableName}
WHERE Id = $Id
LIMIT 1;
";
        cmd.Parameters.AddWithValue("$Id", id);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new Poi
        {
            Id = r.GetInt32(0),
            Name = r.GetString(1),
            Description = r.IsDBNull(2) ? null : r.GetString(2),
            Latitude = r.GetDouble(3),
            Longitude = r.GetDouble(4),
            RadiusMeters = r.GetInt32(5),
            CooldownSeconds = r.GetInt32(6),
            NarrationText = r.IsDBNull(7) ? null : r.GetString(7),
            AudioUrl = r.IsDBNull(8) ? null : r.GetString(8),
            ImageUrl = r.IsDBNull(9) ? null : r.GetString(9),
            MapLink = r.IsDBNull(10) ? null : r.GetString(10),
            IsActive = r.GetInt32(11) == 1,
            CreatedAt = DateTime.Parse(r.GetString(12)),
            UpdatedAt = DateTime.Parse(r.GetString(13)),
            Language = r.IsDBNull(14) ? "vi-VN" : r.GetString(14),
        };
    }
}