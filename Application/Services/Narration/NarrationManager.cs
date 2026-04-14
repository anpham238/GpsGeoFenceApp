using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Services.Audio;
using Microsoft.Maui.Media;
using System.Text.RegularExpressions;
namespace MauiApp1.Services.Narration;

public enum PoiEventType { Enter, Near, Tap }
// ĐỔI CHỮ 'PoiLang' THÀNH 'string'
public record Announcement(Poi Poi, string Lang, PoiEventType EventType, DateTime CreatedAtUtc, string? OverrideText = null)
{
    public string ResolvedLanguage =>
        Lang
        ?? TryGetCurrentLanguage()
        ?? "vi-VN";

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

    public async Task HandleAsync(Announcement ann, string? overrideText = null, CancellationToken ct = default)
    {
        Stop();

        _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _currentCts.Token;

        try
        {
            // 1) Ưu tiên phát audio nếu có
            if (!string.IsNullOrWhiteSpace(ann.Poi.AudioUrl))
            {
                var localPath = await _cache.GetOrAddFromUrlAsync(ann.Poi.AudioUrl!, token);
                if (!string.IsNullOrWhiteSpace(localPath))
                {
                    await _player.PlayFileAsync(localPath!, token);
                    return;
                }
            }

            // 2) TTS: đọc text đã dịch (overrideText) nếu có
            var text = !string.IsNullOrWhiteSpace(overrideText)
                ? overrideText!
                : (!string.IsNullOrWhiteSpace(ann.Poi.NarrationText)
                    ? ann.Poi.NarrationText!
                    : ComposeFallbackText(ann));

            var lang = ann.ResolvedLanguage;

            var options = new SpeechOptions { Volume = 1.0f, Pitch = 1.0f };
            var locale = await FindLocaleAsync(lang, token);
            if (locale is not null) options.Locale = locale;

            foreach (var part in SplitToParts(text))
            {
                token.ThrowIfCancellationRequested();
                await TextToSpeech.Default.SpeakAsync(part, options, token);
                await Task.Delay(400, token); // ✅ pause 500ms
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Narration] Error: {ex}");
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
        var lang = ann.ResolvedLanguage; // Lấy ngôn ngữ hiện tại (ví dụ: en-US, ja-JP)

        // 1. Gán tiếng Việt làm mặc định
        string textNear = "Bạn sắp đến";
        string textEnter = "Bạn đã đến";

        // 2. Tự động đổi câu chào theo ngôn ngữ đang chọn
        if (lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            textNear = "You are approaching";
            textEnter = "You have arrived at";
        }
        else if (lang.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            textNear = "まもなく到着します"; // Mamonaku tōchaku shimasu
            textEnter = "到着しました";       // Tōchaku shimashita
        }
        else if (lang.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            textNear = "곧 도착합니다";   // Got dochakhamnida
            textEnter = "도착했습니다";   // Dochakhaetseumnida
        }
        else if (lang.StartsWith("de", StringComparison.OrdinalIgnoreCase))
        {
            textNear = "Sie nähern sich";
            textEnter = "Sie haben erreicht";
        }

        // 3. Ghép câu chào + Tên địa điểm + Mô tả
        return ann.EventType switch
        {
            PoiEventType.Near => $"{textNear} {name}.",
            PoiEventType.Enter or PoiEventType.Tap => string.IsNullOrWhiteSpace(desc)
                ? $"{textEnter} {name}."
                : $"{textEnter} {name}. {desc}",
            _ => name
        };
    }
    private static IEnumerable<string> SplitToParts(string text)
    {
        var lines = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (lines.Count > 1)
            return lines;

        // tách theo dấu câu để tạo nghỉ tự nhiên
        var parts = Regex.Split(text, @"(?<=[\.!\?。！？])\s+")
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        return parts.Count > 0 ? parts : new[] { text };
    }

    private static async Task<Locale?> FindLocaleAsync(string lang, CancellationToken ct)
    {
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var exact = locales.FirstOrDefault(l =>
                string.Equals(l.Language, lang, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact;

            var primary = lang.Split('-')[0];
            return locales.FirstOrDefault(l =>
                l.Language.StartsWith(primary, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return null;
        }
    }
}