using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Services.Audio;
using Microsoft.Maui.Media;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace MauiApp1.Services.Narration;
public enum PoiEventType { Enter, Near, Tap }
public sealed record Announcement(
    Poi Poi,
    PoiEventType EventType,
    DateTime CreatedAtUtc,
    string? PreferredLanguage = null)
{
    public string ResolvedLanguage =>
        PreferredLanguage
        ?? (TryGetCurrentLanguage() ?? "vi-VN");
    private static string? TryGetCurrentLanguage()
    {
        try { return LanguageService.Current; }
        catch { return null; }
    }
}
public sealed class NarrationManager
{
    private readonly IAudioPlayer _player;
    private readonly AudioCache _cache;
    private readonly object _gate = new();
    private CancellationTokenSource? _currentCts;
    public NarrationManager(IAudioPlayer player, AudioCache cache)
    {
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }
    public async Task HandleAsync(Announcement ann, CancellationToken ct = default)
    {
        Stop();
        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _currentCts.Token;
        try
        {
            // 1) Audio URL -> cache -> play
            if (!string.IsNullOrWhiteSpace(ann.Poi.AudioUrl))
            {
                var localPath = await _cache.GetOrAddFromUrlAsync(ann.Poi.AudioUrl!, token);
                if (!string.IsNullOrWhiteSpace(localPath))
                {
                    await _player.PlayFileAsync(localPath!, token);
                    return;
                }
            }

            // 2) Fallback TTS
            var text = !string.IsNullOrWhiteSpace(ann.Poi.NarrationText)
                ? ann.Poi.NarrationText!
                : ComposeFallbackText(ann);

            var options = new SpeechOptions { Volume = 1.0f, Pitch = 1.0f };

            var lang = ann.ResolvedLanguage;
            if (!string.IsNullOrWhiteSpace(lang))
            {
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                var match = locales.FirstOrDefault(l =>
                    string.Equals(l.Language, lang, StringComparison.OrdinalIgnoreCase));

                match ??= locales.FirstOrDefault(l =>
                    l.Language.StartsWith(lang.Split('-')[0], StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    options.Locale = match;
            }

            await TextToSpeech.Default.SpeakAsync(text, options, token);
        }
        catch (OperationCanceledException)
        {
            // ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Narration] Error: {ex.Message}");
        }
        finally
        {
            Stop();
        }
    }
    public void Stop()
    {
        lock (_gate)
        {
            try { _currentCts?.Cancel(); } catch { }
            try { _player.Stop(); } catch { }
            _currentCts?.Dispose();
            _currentCts = null;
        }
    }
    private static string ComposeFallbackText(Announcement ann)
    {
        var name = ann.Poi.Name?.Trim() ?? "";
        var desc = string.IsNullOrWhiteSpace(ann.Poi.Description) ? "" : ann.Poi.Description!.Trim();
        return ann.EventType switch
        {
            PoiEventType.Enter => string.IsNullOrWhiteSpace(desc)
                ? $"Bạn đang ở {name}."
                : $"Bạn đang ở {name}. {desc}",

            PoiEventType.Near => string.IsNullOrWhiteSpace(desc)
                ? $"Bạn sắp đến {name}."
                : $"Bạn sắp đến {name}. {desc}",

            PoiEventType.Tap => string.IsNullOrWhiteSpace(desc)
                ? $"{name}."
                : $"{name}. {desc}",
            _ => name
        };
    }
}
