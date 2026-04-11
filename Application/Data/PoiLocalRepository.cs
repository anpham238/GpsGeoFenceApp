using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MauiApp1.Models;

namespace MauiApp1.Data;

public sealed class PoiLocalRepository
{
    private readonly ILogger _logger;
    private bool _inited;

    public PoiLocalRepository(ILogger<PoiLocalRepository> logger)
    {
        _logger = logger;
    }

    private async Task InitAsync()
    {
        if (_inited) return;

        await using var conn = new SqliteConnection(Constants.DatabasePath);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS PoisLocal(
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT NULL,
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
    ServerUpdatedAt TEXT NOT NULL,
    IsDirty INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_PoisLocal_ActivePriority ON PoisLocal(IsActive, Priority);
";
        await cmd.ExecuteNonQueryAsync();
        _inited = true;
    }

    public async Task<List<PoiLocal>> GetAllAsync(bool onlyActive = true)
    {
        await InitAsync();
        var list = new List<PoiLocal>();

        await using var conn = new SqliteConnection(Constants.DatabasePath);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = onlyActive
            ? "SELECT * FROM PoisLocal WHERE IsActive=1 AND IsDeleted=0 ORDER BY Priority, Name;"
            : "SELECT * FROM PoisLocal WHERE IsDeleted=0 ORDER BY Priority, Name;";

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadPoi(reader));
        }
        return list;
    }

    public async Task<List<PoiLocal>> GetDirtyAsync()
    {
        await InitAsync();
        var list = new List<PoiLocal>();

        await using var conn = new SqliteConnection(Constants.DatabasePath);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM PoisLocal WHERE IsDirty=1;";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(ReadPoi(reader));
        }
        return list;
    }

    public async Task UpsertManyFromServerAsync(IEnumerable<PoiDto> remotePois)
    {
        await InitAsync();

        await using var conn = new SqliteConnection(Constants.DatabasePath);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        foreach (var r in remotePois)
        {
            // Nếu local đã sửa (IsDirty=1), không overwrite (tránh mất dữ liệu).
            // Đây là chiến lược an toàn mặc định (có thể đổi sang prefer-remote sau).
            var check = conn.CreateCommand();
            check.Transaction = (SqliteTransaction?)tx;
            check.CommandText = "SELECT IsDirty, ServerUpdatedAt FROM PoisLocal WHERE Id=$id;";
            check.Parameters.AddWithValue("$id", r.Id);

            int? isDirty = null;
            DateTime? localServerUpdatedAt = null;

            await using (var rr = await check.ExecuteReaderAsync())
            {
                if (await rr.ReadAsync())
                {
                    isDirty = rr.GetInt32(rr.GetOrdinal("IsDirty"));
                    localServerUpdatedAt = DateTime.Parse(rr.GetString(rr.GetOrdinal("ServerUpdatedAt")));
                }
            }

            if (isDirty == 1)
                continue; // giữ local edits

            // Nếu serverUpdatedAt không mới hơn thì bỏ qua (delta kiểu timestamp)
            if (localServerUpdatedAt is not null && r.UpdatedAt <= localServerUpdatedAt.Value)
                continue;

            var up = conn.CreateCommand();
            up.Transaction = (SqliteTransaction?)tx;
            up.CommandText = @"
INSERT INTO PoisLocal(
  Id, Name, Description, Latitude, Longitude,
  RadiusMeters, NearRadiusMeters, DebounceSeconds, CooldownSeconds,
  Priority, NarrationText, AudioUrl, ImageUrl, MapLink, IsActive,
  ServerUpdatedAt, IsDirty, IsDeleted
) VALUES (
  $Id, $Name, $Description, $Latitude, $Longitude,
  $RadiusMeters, $NearRadiusMeters, $DebounceSeconds, $CooldownSeconds,
  $Priority, $NarrationText, $AudioUrl, $ImageUrl, $MapLink, $IsActive,
  $ServerUpdatedAt, 0, 0
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
  ServerUpdatedAt=excluded.ServerUpdatedAt,
  IsDeleted=0
;";
            up.Parameters.AddWithValue("$Id", r.Id);
            up.Parameters.AddWithValue("$Name", r.Name);
            up.Parameters.AddWithValue("$Description", (object?)r.Description ?? DBNull.Value);
            up.Parameters.AddWithValue("$Latitude", r.Latitude);
            up.Parameters.AddWithValue("$Longitude", r.Longitude);
            up.Parameters.AddWithValue("$RadiusMeters", r.RadiusMeters);
            up.Parameters.AddWithValue("$NearRadiusMeters", r.NearRadiusMeters);
            up.Parameters.AddWithValue("$DebounceSeconds", r.DebounceSeconds);
            up.Parameters.AddWithValue("$CooldownSeconds", r.CooldownSeconds);
            up.Parameters.AddWithValue("$NarrationText", (object?)r.NarrationText ?? DBNull.Value);
            up.Parameters.AddWithValue("$ImageUrl", (object?)r.ImageUrl ?? DBNull.Value);
            up.Parameters.AddWithValue("$MapLink", (object?)r.MapLink ?? DBNull.Value);
            up.Parameters.AddWithValue("$IsActive", r.IsActive ? 1 : 0);
            up.Parameters.AddWithValue("$ServerUpdatedAt", r.UpdatedAt.ToString("O"));

            await up.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    public async Task MarkCleanAsync(string id, DateTime serverUpdatedAt)
    {
        await InitAsync();
        await using var conn = new SqliteConnection(Constants.DatabasePath);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE PoisLocal SET IsDirty=0, ServerUpdatedAt=$t WHERE Id=$id;";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$t", serverUpdatedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }

    private static PoiLocal ReadPoi(SqliteDataReader r)
    {
        int Ord(string n) => r.GetOrdinal(n);

        return new PoiLocal
        {
            Id = r.GetString(Ord("Id")),
            Name = r.GetString(Ord("Name")),
            Description = r.IsDBNull(Ord("Description")) ? null : r.GetString(Ord("Description")),
            Latitude = r.GetDouble(Ord("Latitude")),
            Longitude = r.GetDouble(Ord("Longitude")),
            RadiusMeters = r.GetInt32(Ord("RadiusMeters")),
            NearRadiusMeters = r.GetInt32(Ord("NearRadiusMeters")),
            DebounceSeconds = r.GetInt32(Ord("DebounceSeconds")),
            CooldownSeconds = r.GetInt32(Ord("CooldownSeconds")),
            Priority = r.IsDBNull(Ord("Priority")) ? null : r.GetInt32(Ord("Priority")),
            NarrationText = r.IsDBNull(Ord("NarrationText")) ? null : r.GetString(Ord("NarrationText")),
            AudioUrl = r.IsDBNull(Ord("AudioUrl")) ? null : r.GetString(Ord("AudioUrl")),
            ImageUrl = r.IsDBNull(Ord("ImageUrl")) ? null : r.GetString(Ord("ImageUrl")),
            MapLink = r.IsDBNull(Ord("MapLink")) ? null : r.GetString(Ord("MapLink")),
            IsActive = r.GetInt32(Ord("IsActive")) == 1,
            ServerUpdatedAt = DateTime.Parse(r.GetString(Ord("ServerUpdatedAt"))),
            IsDirty = r.GetInt32(Ord("IsDirty")) == 1,
            IsDeleted = r.GetInt32(Ord("IsDeleted")) == 1
        };
    }
}