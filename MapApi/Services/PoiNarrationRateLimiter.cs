using System.Collections.Concurrent;

namespace MapApi.Services;

/// <summary>
/// Per-POI rate limiter để tránh dồn request khi nhiều người cùng vào một POI.
/// Mỗi POI có slot đồng thời tối đa, kết hợp delay nhỏ để trải đều tải.
/// </summary>
public sealed class PoiNarrationRateLimiter
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _slots = new();

    private const int MaxConcurrentPerPoi = 10;
    private const int SpreadDelayMs = 80;

    public async Task<bool> TryAcquireAsync(int poiId, CancellationToken ct = default)
    {
        var sem = _slots.GetOrAdd(poiId, _ => new SemaphoreSlim(MaxConcurrentPerPoi, MaxConcurrentPerPoi));

        // Nếu không còn slot → từ chối (trả 429 để client thử lại)
        if (!sem.Wait(0)) return false;

        // Delay nhỏ để trải đều tải khi nhiều request cùng lúc
        try { await Task.Delay(SpreadDelayMs, ct); }
        catch (OperationCanceledException) { sem.Release(); throw; }

        return true;
    }

    public void Release(int poiId)
    {
        if (_slots.TryGetValue(poiId, out var sem))
            sem.Release();
    }
}
