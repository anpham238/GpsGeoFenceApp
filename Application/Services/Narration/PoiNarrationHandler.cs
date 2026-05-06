using MauiApp1.Models;
using MauiApp1.Services.Api;

namespace MauiApp1.Services.Narration;

/// <summary>
/// Gom logic: fetch narration text → play → log analytics/playback.
/// Có debounce per-POI để tránh gửi lặp request trong vài trăm ms.
/// </summary>
public class PoiNarrationHandler(
    NarrationManager narration,
    PoiNarrationApiClient narrationApi,
    PoiNarrationCache narrationCache,
    PlaybackApiClient playback,
    AnalyticsClient analytics)
{
    // Debounce: không gửi lại request narration cho cùng 1 POI trong khoảng này
    private const int DebounceMs = 250;
    private readonly Dictionary<int, DateTime> _lastRequestTime = new();
    private readonly object _debounceLock = new();

    public void Stop() => narration.Stop();

    public void LogRoute(double lat, double lng) =>
        _ = analytics.LogRouteAsync(lat, lng);

    public async Task PlayAsync(Poi poi, PoiEventType evType, string logLabel, CancellationToken ct = default)
    {
        if (IsDebounced(poi.Id)) return;

        var started = DateTime.UtcNow;
        var lang    = LanguageService.Current;

        var fullText = await FetchTextAsync(poi, evType, lang, ct);
        await narration.HandleAsync(new Announcement(poi, lang, evType, started), overrideText: fullText);

        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
        _ = playback.LogAsync(poi.Id, logLabel, dur > 0 ? dur : null);
        _ = analytics.LogVisitAsync(poi.Id, logLabel.ToLowerInvariant());
        if (dur > 0) _ = analytics.LogListenDurationAsync(poi.Id, dur);
    }

    private bool IsDebounced(int poiId)
    {
        var now = DateTime.UtcNow;
        lock (_debounceLock)
        {
            if (_lastRequestTime.TryGetValue(poiId, out var last) &&
                (now - last).TotalMilliseconds < DebounceMs)
                return true;
            _lastRequestTime[poiId] = now;
            return false;
        }
    }

    private async Task<string?> FetchTextAsync(Poi poi, PoiEventType evType, string lang, CancellationToken ct)
    {
        try
        {
            var evByte = ToEventByte(evType);
            var cached = await narrationCache.GetAsync(poi.Id, evByte, lang);
            if (!string.IsNullOrWhiteSpace(cached)) return cached;

            if (Connectivity.Current.NetworkAccess != NetworkAccess.None)
            {
                var dto  = await narrationApi.GetNarrationAsync(poi.Id, lang, ToEventName(evType), ct);
                var text = dto?.NarrationText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await narrationCache.UpsertAsync(poi.Id, evByte, dto!.Language, text);
                    return text;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationFetch] {ex.Message}");
        }
        return poi.NarrationText ?? poi.Description;
    }

    private static byte   ToEventByte(PoiEventType t) => t switch { PoiEventType.Enter => 0, PoiEventType.Near => 1, PoiEventType.Tap => 2, _ => 0 };
    private static string ToEventName(PoiEventType t) => t switch { PoiEventType.Enter => "Enter", PoiEventType.Near => "Near", PoiEventType.Tap => "Tap", _ => "Enter" };
}
