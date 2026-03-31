using Microsoft.Maui.Media;
using MauiApp1.Models;
using MauiApp1.Services.Audio;

namespace MauiApp1.Services.Narration;

public enum PoiEventType { Enter, Near, Tap }

public sealed record Announcement(
    Poi Poi,
    PoiEventType EventType,
    DateTime CreatedAtUtc,
    string? PreferredLanguage = null // "vi-VN", "en-US", ...
);

public sealed class NarrationManager
{
    private readonly IAudioPlayer _player;
    private readonly AudioCache _cache;

    private readonly object _gate = new();
    private bool _isPlaying;
    private DateTime _startedAtUtc;

    public NarrationManager(IAudioPlayer player, AudioCache cache)
    {
        _player = player;
        _cache = cache;
    }

    // Ưu tiên: số Priority lớn → quan trọng hơn; loại sự kiện: Enter > Near > Tap
    private static int Score(Announcement a)
    {
        var p = a.Poi.Priority ?? 1;
        var t = a.EventType switch { PoiEventType.Enter => 3, PoiEventType.Near => 2, PoiEventType.Tap => 1, _ => 0 };
        return p * 10 + t;
    }

    public async Task HandleAsync(Announcement ann, CancellationToken ct = default)
    {
        lock (_gate)
        {
            if (_isPlaying)
            {
                // Demo: không chen ngang. Muốn preempt thì so sánh Score(ann) với “đang phát”
                return;
            }
            _isPlaying = true;
            _startedAtUtc = DateTime.UtcNow;
        }

        try
        {
            // 1) Thử audio URL trước
            if (!string.IsNullOrWhiteSpace(ann.Poi.AudioUrl))
            {
                var local = await _cache.GetOrAddFromUrlAsync(ann.Poi.AudioUrl!, ct);
                if (!string.IsNullOrEmpty(local))
                {
                    await _player.PlayFileAsync(local!, ct);
                    return;
                }
            }

            // 2) Fallback TTS (đa ngôn ngữ)
            var text = !string.IsNullOrWhiteSpace(ann.Poi.NarrationText)
                ? ann.Poi.NarrationText!
                : $"Bạn đang đến {ann.Poi.Name}. {ann.Poi.Description}";

            var opts = new SpeechOptions { Volume = 1.0f, Pitch = 1.0f };

            if (!string.IsNullOrWhiteSpace(ann.PreferredLanguage))
            {
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                var match = locales.FirstOrDefault(l =>
                    string.Equals(l.Language, ann.PreferredLanguage, StringComparison.OrdinalIgnoreCase));
                if (match != null) opts.Locale = match;
            }

            await TextToSpeech.Default.SpeakAsync(text, opts);
        }
        finally
        {
            lock (_gate) _isPlaying = false;
        }
    }
}
