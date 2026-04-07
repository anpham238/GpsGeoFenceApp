using Microsoft.Data.Sqlite;

namespace MauiApp1.Data;

public sealed class PoiNarrationCache
{
    private bool _inited;

    private async Task InitAsync()
    {
        if (_inited) return;

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS PoiNarrationCache(
  PoiId TEXT NOT NULL,
  EventType INTEGER NOT NULL,
  LanguageTag TEXT NOT NULL,
  NarrationText TEXT NULL,
  UpdatedAtUtc TEXT NOT NULL,
  PRIMARY KEY(PoiId, EventType, LanguageTag)
);";
        await cmd.ExecuteNonQueryAsync();
        _inited = true;
    }

    public async Task<string?> GetAsync(string poiId, byte eventType, string lang)
    {
        await InitAsync();

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT NarrationText
FROM PoiNarrationCache
WHERE PoiId=$p AND EventType=$e AND LanguageTag=$l
LIMIT 1;";
        cmd.Parameters.AddWithValue("$p", poiId);
        cmd.Parameters.AddWithValue("$e", (int)eventType);
        cmd.Parameters.AddWithValue("$l", lang);

        var obj = await cmd.ExecuteScalarAsync();
        return obj == null || obj == DBNull.Value ? null : (string)obj;
    }

    public async Task UpsertAsync(string poiId, byte eventType, string lang, string? text)
    {
        await InitAsync();

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO PoiNarrationCache(PoiId, EventType, LanguageTag, NarrationText, UpdatedAtUtc)
VALUES($p, $e, $l, $t, $u)
ON CONFLICT(PoiId, EventType, LanguageTag) DO UPDATE SET
  NarrationText=excluded.NarrationText,
  UpdatedAtUtc=excluded.UpdatedAtUtc;";
        cmd.Parameters.AddWithValue("$p", poiId);
        cmd.Parameters.AddWithValue("$e", (int)eventType);
        cmd.Parameters.AddWithValue("$l", lang);
        cmd.Parameters.AddWithValue("$t", (object?)text ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("O"));

        await cmd.ExecuteNonQueryAsync();
    }
}