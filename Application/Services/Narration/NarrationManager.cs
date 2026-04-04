using MauiApp1.Models;
using MauiApp1.Services; // <-- nếu IAudioPlayer nằm ở MauiApp1.Services
using Microsoft.Maui.Media;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MauiApp1.Services.Audio; 
namespace MauiApp1.Services.Narration
{
    public enum PoiEventType { Enter, Near, Tap }

    public sealed record Announcement(
        Poi Poi,
        PoiEventType EventType,
        DateTime CreatedAtUtc,
        string? PreferredLanguage = "vi-VN" // mặc định đọc tiếng Việt
    );

    /// <summary>
    /// NarrationManager: ưu tiên phát file AudioUrl, nếu không có thì fallback Text-to-Speech.
    /// Mỗi lần phát sẽ dừng nội dung cũ để tránh chồng tiếng.
    /// </summary>
    public sealed class NarrationManager : INarrationManager
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
            // Dừng nội dung đang phát (nếu có)
            Stop();

            // Liên kết token ngoài với token nội bộ để có thể hủy
            _currentCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _currentCts.Token;

            try
            {
                // 1) Thử phát file audio nếu có URL
                if (!string.IsNullOrWhiteSpace(ann.Poi.AudioUrl))
                {
                    var localPath = await _cache.GetOrAddFromUrlAsync(ann.Poi.AudioUrl!, token);
                    if (!string.IsNullOrWhiteSpace(localPath))
                    {
                        await _player.PlayFileAsync(localPath!, token);
                        return;
                    }
                }

                // 2) Fallback TTS (nếu không có AudioUrl hoặc tải lỗi)
                var text = !string.IsNullOrWhiteSpace(ann.Poi.NarrationText)
                           ? ann.Poi.NarrationText!
                           : ComposeFallbackText(ann);

                var options = new SpeechOptions { Volume = 1.0f, Pitch = 1.0f };

                if (!string.IsNullOrWhiteSpace(ann.PreferredLanguage))
                {

                    var locales = await TextToSpeech.Default.GetLocalesAsync();
                    var lang = ann.PreferredLanguage ?? "vi-VN";
                    // match chính xác trước
                    var match = locales.FirstOrDefault(l =>string.Equals(l.Language, lang, StringComparison.OrdinalIgnoreCase));
                    match ??= locales.FirstOrDefault(l =>
                        l.Language.StartsWith(lang.Split('-')[0], StringComparison.OrdinalIgnoreCase));

                    if (match is not null) options.Locale = match;
                }

                await TextToSpeech.Default.SpeakAsync(text, options, token);
            }
            catch (OperationCanceledException)
            {
                // bị hủy khi Stop() hoặc token ngoài cancel
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Narration] Error: {ex.Message}");
            }
            finally
            {
                Stop(); // dọn CTS & player
            }
        }
        public void Stop()
        {
            lock (_gate)
            {
                try { _currentCts?.Cancel(); } catch { /* ignore */ }
                try { _player.Stop(); } catch { /* ignore */ }

                _currentCts?.Dispose();
                _currentCts = null;
            }
        }
        private static string ComposeFallbackText(Announcement ann)
        {
            var name = ann.Poi.Name?.Trim();
            var desc = string.IsNullOrWhiteSpace(ann.Poi.Description) ? "" : ann.Poi.Description.Trim();

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

                _ => name ?? ""
            };
        }
    }
}