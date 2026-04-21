using MapApi.Data;
using MapApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Services;

public interface IHistoryService
{
    Task<HistoryUpsertResult> UpsertVisitAsync(int poiId, Guid userId, int? durationSeconds, CancellationToken ct = default);
}

public sealed record HistoryUpsertResult(bool Success, string? Error = null);

public sealed class HistoryService(AppDb db) : IHistoryService
{
    public async Task<HistoryUpsertResult> UpsertVisitAsync(int poiId, Guid userId, int? durationSeconds, CancellationToken ct = default)
    {
        if (poiId <= 0 || userId == Guid.Empty)
            return new HistoryUpsertResult(false, "PoiId và UserId là bắt buộc");

        var poi = await db.Pois.AsNoTracking().FirstOrDefaultAsync(p => p.Id == poiId, ct);
        if (poi is null) return new HistoryUpsertResult(false, "POI not found");

        if (!await db.Users.AnyAsync(u => u.UserId == userId, ct))
            return new HistoryUpsertResult(false, "User not found");

        var existing = await db.HistoryPoi
            .FirstOrDefaultAsync(h => h.IdPoi == poiId && h.IdUser == userId, ct);

        if (existing is not null)
        {
            existing.Quantity++;
            existing.LastVisitedAt = DateTime.UtcNow;
            existing.TotalDurationSeconds =
                (existing.TotalDurationSeconds ?? 0) + (durationSeconds ?? 0);
        }
        else
        {
            db.HistoryPoi.Add(new HistoryPoi
            {
                IdPoi = poiId,
                IdUser = userId,
                PoiName = poi.Name,
                Quantity = 1,
                LastVisitedAt = DateTime.UtcNow,
                TotalDurationSeconds = durationSeconds
            });
        }

        await db.SaveChangesAsync(ct);
        return new HistoryUpsertResult(true);
    }
}
