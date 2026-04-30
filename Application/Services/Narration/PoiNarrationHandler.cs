using MauiApp1.Models;
using MauiApp1.Services.Api;

namespace MauiApp1.Services.Narration;

/// <summary>
/// Gom logic: fetch narration text → play → log analytics/playback.
/// Loại bỏ duplicate code trong MapPage (5 nơi gọi cùng pattern).
/// </summary>
public class PoiNarrationHandler(
    NarrationManager narration,
    PoiNarrationApiClient narrationApi,
    PoiNarrationCache narrationCache,
    PlaybackApiClient playback,
    AnalyticsClient analytics)
{
    public void Stop() => narration.Stop();

    public void LogRoute(double lat, double lng) =>
        _ = analytics.LogRouteAsync(lat, lng);

    public async Task PlayAsync(Poi poi, PoiEventType evType, string logLabel, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var lang    = LanguageService.Current;

        var fullText = await FetchTextAsync(poi, evType, lang, ct);
        await narration.HandleAsync(new Announcement(poi, lang, evType, started), overrideText: fullText);

        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
        _ = playback.LogAsync(poi.Id, logLabel, dur > 0 ? dur : null);
        _ = analytics.LogVisitAsync(poi.Id, logLabel.ToLowerInvariant());
        if (dur > 0) _ = analytics.LogListenDurationAsync(poi.Id, dur);
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
