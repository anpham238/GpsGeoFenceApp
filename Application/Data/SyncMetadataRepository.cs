using Microsoft.Data.Sqlite;

namespace MauiApp1.Data;

public sealed class SyncMetadataRepository
{
    private bool _inited;

    private async Task InitAsync()
    {
        if (_inited) return;

        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS SyncMeta(
  Key TEXT PRIMARY KEY,
  Value TEXT NOT NULL
);";
        await cmd.ExecuteNonQueryAsync();
        _inited = true;
    }

    public async Task SetLastSyncUtcAsync(string scope, DateTime utc)
    {
        await InitAsync();
        await using var conn = new SqliteConnection(Constants.ConnectionString);
        await conn.OpenAsync();

        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO SyncMeta(Key, Value) VALUES ($k, $v)
ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value;";
        cmd.Parameters.AddWithValue("$k", $"LastSyncUtc:{scope}");
        cmd.Parameters.AddWithValue("$v", utc.ToString("O"));
        await cmd.ExecuteNonQueryAsync();
    }
}