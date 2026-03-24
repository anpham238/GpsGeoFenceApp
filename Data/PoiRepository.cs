using Microsoft.Data.SqlClient;
using MauiApp1.Models;
using System.Diagnostics;

namespace MauiApp1.Data;

/// <summary>
/// Ket noi truc tiep tu MAUI app len Azure SQL Server.
/// Dung Microsoft.Data.SqlClient (ho tro Android/iOS).
/// 
/// HUONG DAN SETUP:
/// 1. NuGet: Install-Package Microsoft.Data.SqlClient -Version 5.2.2
/// 2. Them connection string vao appsettings.json hoac Constants.cs
/// 3. Dang ky Singleton trong MauiProgram.cs:
///    builder.Services.AddSingleton<PoiDbService>();
/// </summary>
public class PoiDbService
{
    // ── Connection String ────────────────────────────────────────────────
    // THAY the cac gia tri nay bang thong tin Azure SQL cua ban:
    // Lay tu: Azure Portal → SQL Database → Connection strings → ADO.NET
    private readonly string _connectionString;

    public PoiDbService()
    {
        // Doc tu DbConstants hoac cau hinh cung
        _connectionString = DbConstants.AzureSqlConnectionString;
    }

    // ── Tao connection ───────────────────────────────────────────────────
    private SqlConnection CreateConnection()
        => new SqlConnection(_connectionString);

    // ── Kiem tra ket noi ────────────────────────────────────────────────
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            Debug.WriteLine("[DB] Ket noi Azure SQL thanh cong!");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB] Loi ket noi: {ex.Message}");
            return false;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // POI CRUD
    // ════════════════════════════════════════════════════════════════════

    /// <summary>Lay tat ca POI dang hoat dong tu Azure SQL.</summary>
    public async Task<List<Poi>> GetActivePoisAsync()
    {
        var result = new List<Poi>();

        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_GetActivePois", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(MapToPoi(reader));

            Debug.WriteLine($"[DB] Da tai {result.Count} POI tu Azure SQL");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB] GetActivePois loi: {ex.Message}");
        }

        return result;
    }

    /// <summary>Lay 1 POI theo Id.</summary>
    public async Task<Poi?> GetPoiByIdAsync(string id)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_GetPoiById", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            cmd.Parameters.AddWithValue("@Id", id);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return MapToPoi(reader);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB] GetPoiById loi: {ex.Message}");
        }

        return null;
    }

    /// <summary>Them moi POI vao Azure SQL.</summary>
    public async Task<bool> InsertPoiAsync(Poi poi)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_InsertPoi", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Id", poi.Id);
            cmd.Parameters.AddWithValue("@Name", poi.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)poi.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Latitude", poi.Latitude);
            cmd.Parameters.AddWithValue("@Longitude", poi.Longitude);
            cmd.Parameters.AddWithValue("@RadiusMeters", poi.RadiusMeters);
            cmd.Parameters.AddWithValue("@NearRadiusMeters", poi.NearRadiusMeters);
            cmd.Parameters.AddWithValue("@NarrationText", (object?)poi.NarrationText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AudioUrl", (object?)poi.AudioUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ImageUrl", (object?)poi.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MapLink", (object?)poi.MapLink ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Priority", poi.Priority);

            await cmd.ExecuteNonQueryAsync();
            Debug.WriteLine($"[DB] Da them POI: {poi.Name}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB] InsertPoi loi: {ex.Message}");
            return false;
        }
    }

    /// <summary>Cap nhat POI da co.</summary>
    public async Task<bool> UpdatePoiAsync(Poi poi)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            await using var cmd = new SqlCommand("sp_UpdatePoi", conn)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };

            cmd.Parameters.AddWithValue("@Id", poi.Id);
            cmd.Parameters.AddWithValue("@Name", poi.Name);
            cmd.Parameters.AddWithValue("@Description", (object?)poi.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Latitude", poi.Latitude);
            cmd.Parameters.AddWithValue("@Longitude", poi.Longitude);
            cmd.Parameters.AddWithValue("@RadiusMeters", poi.RadiusMeters);
            cmd.Parameters.AddWithValue("@NearRadiusMeters", poi.NearRadiusMeters);
            cmd.Parameters.AddWithValue("@NarrationText", (object?)poi.NarrationText ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@AudioUrl", (object?)poi.AudioUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ImageUrl", (object?)poi.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MapLink", (object?)poi.MapLink ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IsActive", poi.IsActive);
            cmd.Parameters.AddWithValue("@Priority", poi.Priority);

            await cmd.ExecuteNonQueryAsync();
            Debug.WriteLine($"[DB] Da cap nhat POI: {poi.Id}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB] UpdatePoi loi: {ex.Message}");
            return false;
        }
    }

    /// <summary>Xoa mem POI (IsActive = false).</summary>
    public async Task<bool> DeletePoiAsync(string id)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            const string sql = "UPDATE POIs SET IsActive = 0, UpdatedAt = GETUTCDATE() WHERE Id = @Id";
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", id);
            await cmd.ExecuteNonQueryAsync();

            Debug.WriteLine($"[DB] Da xoa mem POI: {id}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DB] DeletePoi loi: {ex.Message}");
            return false;
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // MAP READER → POI
    // ════════════════════════════════════════════════════════════════════
    private static Poi MapToPoi(SqlDataReader r)
    {
        string? SafeStr(string col)
        {
            int i = r.GetOrdinal(col);
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        return new Poi
        {
            Id = r.GetString(r.GetOrdinal("Id")),
            Name = r.GetString(r.GetOrdinal("Name")),
            Description = SafeStr("Description") ?? string.Empty,
            Latitude = (double)r.GetDecimal(r.GetOrdinal("Latitude")),
            Longitude = (double)r.GetDecimal(r.GetOrdinal("Longitude")),
            RadiusMeters = (float)r.GetDouble(r.GetOrdinal("RadiusMeters")),
            NearRadiusMeters = (float)r.GetDouble(r.GetOrdinal("NearRadiusMeters")),
            DebounceSeconds = r.GetInt32(r.GetOrdinal("DebounceSeconds")),
            CooldownSeconds = r.GetInt32(r.GetOrdinal("CooldownSeconds")),
            // Uu tien AudioUrl tu AudioContents, fallback sang POIs.AudioUrl
            AudioUrl = SafeStr("DefaultAudioUrl") ?? SafeStr("AudioUrl"),
            NarrationText = SafeStr("DefaultTtsScript") ?? SafeStr("NarrationText"),
            ImageUrl = SafeStr("ImageUrl"),
            MapLink = SafeStr("MapLink"),
            IsActive = r.GetBoolean(r.GetOrdinal("IsActive")),
            Priority = r.GetInt32(r.GetOrdinal("Priority")),
        };
    }
}