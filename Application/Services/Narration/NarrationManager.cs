using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Services.Audio;
using Microsoft.Maui.Media;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace MauiApp1.Services.Narration;

public enum PoiEventType { Enter, Near, Tap }
public record Announcement(Poi Poi, string Lang, PoiEventType EventType, DateTime CreatedAtUtc);
public sealed class NarrationManager : INarrationManager
{
    private readonly IAudioPlayer _player;
    private readonly AudioCache _cache;
    private readonly object _sync = new();
    private readonly Queue<QueueItem> _queue = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly Dictionary<string, DateTimeOffset> _recentEvents = new();
    private readonly CancellationTokenSource _serviceCts = new();
    private readonly Task _worker;
    private CancellationTokenSource? _currentCts;
    public NarrationManager(IAudioPlayer player, AudioCache cache)
    {
        _player = player;
        _cache = cache;
        _worker = Task.Run(() => WorkerLoopAsync(_serviceCts.Token));
    }
    public void Stop()
    {
        Queue<QueueItem> pending;
        lock (_sync)
        {
            pending = new Queue<QueueItem>(_queue);
            _queue.Clear();
            _currentCts?.Cancel();
        }

        foreach (var item in pending)
            item.TryCancel();

        _player.Stop();
    }

    public async Task HandleAsync(Announcement ann, string? overrideText = null, CancellationToken ct = default)
    {
        if (IsDuplicate(ann)) return;

        var item = new QueueItem(ann, overrideText, ct);
        lock (_sync) _queue.Enqueue(item);
        _queueSignal.Release();
        await item.Completion.ConfigureAwait(false);
    }

    private async Task WorkerLoopAsync(CancellationToken serviceCt)
    {
        while (!serviceCt.IsCancellationRequested)
        {
            try
            {
                await _queueSignal.WaitAsync(serviceCt).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            QueueItem? next = null;
            lock (_sync)
            {
                if (_queue.Count > 0)
                    next = _queue.Dequeue();
            }
            if (next is null) continue;

            if (next.IsCancelled)
            {
                next.TryCancel();
                continue;
            }

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(serviceCt, next.ExternalToken);
            lock (_sync) _currentCts = linkedCts;

            try
            {
                await PlayOneAsync(next.Announcement, next.OverrideText, linkedCts.Token).ConfigureAwait(false);
                next.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                next.TryCancel();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NarrationWorker] {ex.Message}");
                next.TrySetResult();
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_currentCts, linkedCts))
                        _currentCts = null;
                }
            }
        }
    }

    private async Task PlayOneAsync(Announcement ann, string? overrideText, CancellationToken token)
    {
        try
        {
            // 1. Ưu tiên phát Audio nếu có URL
            if (!string.IsNullOrWhiteSpace(ann.Poi.AudioUrl))
            {
                await _player.PlayFileAsync(ann.Poi.AudioUrl, token);
                return;
            }

            // 2. Không có Audio -> Đọc TTS (Ưu tiên Kịch bản lấy từ Database/API, nếu trống thì tự tạo câu)
            var textToSpeak = !string.IsNullOrWhiteSpace(overrideText) 
                              ? overrideText 
                              : ComposeFallbackText(ann);

            var locale = await FindLocaleAsync(ann.Lang, token);
            var options = new SpeechOptions { Locale = locale, Pitch = 1.0f, Volume = 1.0f };

            // Chia nhỏ câu để đọc tự nhiên hơn
            foreach (var part in SplitToParts(textToSpeak))
            {
                if (token.IsCancellationRequested) break;
                await TextToSpeech.Default.SpeakAsync(part, options, token);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TTS Error] {ex.Message}"); }
    }

    private bool IsDuplicate(Announcement ann)
    {
        var now = DateTimeOffset.UtcNow;
        var key = $"{ann.Poi.Id}:{ann.EventType}:{ann.Lang}";
        lock (_sync)
        {
            if (_recentEvents.TryGetValue(key, out var last) &&
                (now - last).TotalSeconds < 2)
            {
                return true;
            }
            _recentEvents[key] = now;
            return false;
        }
    }

    private static string ComposeFallbackText(Announcement ann)
    {
        var name = ann.Poi.Name?.Trim() ?? "";
        var desc = string.IsNullOrWhiteSpace(ann.Poi.Description) ? "" : ann.Poi.Description!.Trim();
        var lang = ann.Lang ?? "vi-VN";

        // 1. Mặc định là Tiếng Việt
        string textNear = "Bạn sắp đến";
        string textEnter = "Bạn đã đến";
        string textTap = "Bạn đang xem thông tin về";

        // 2. Tự động đổi theo Đa ngôn ngữ
        if (lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            textNear = "You are approaching";
            textEnter = "You have arrived at";
            textTap = "You are viewing information for";
        }
        else if (lang.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            textNear = "まもなく到着します"; 
            textEnter = "到着しました";       
            textTap = "の詳細を表示しています"; 
        }
        else if (lang.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            textNear = "곧 도착합니다";   
            textEnter = "도착했습니다";   
            textTap = "정보를 보고 계십니다"; 
        }
        else if (lang.StartsWith("de", StringComparison.OrdinalIgnoreCase))
        {
            textNear = "Sie nähern sich";
            textEnter = "Sie sind angekommen in";
            textTap = "Sie sehen sich Informationen an über";
        }

        // 3. Lắp ráp thành câu hoàn chỉnh dựa trên hành động (Sắp đến / Đã đến / Bấm vào)
        string sentence = ann.EventType switch
        {
            PoiEventType.Near => $"{textNear} {name}.",
            PoiEventType.Tap => $"{textTap} {name}.",
            _ => $"{textEnter} {name}." // Mặc định là Enter (Đã đến)
        };

        // Xử lý ngữ pháp riêng cho tiếng Nhật và tiếng Hàn (Tên địa điểm đứng trước động từ)
        if (lang.StartsWith("ja", StringComparison.OrdinalIgnoreCase)) 
        {
            sentence = ann.EventType == PoiEventType.Tap ? $"{name}{textTap}." : $"{name}に{textEnter}.";
        }
        else if (lang.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) 
        {
            sentence = ann.EventType == PoiEventType.Tap ? $"{name} {textTap}." : $"{name}에 {textEnter}.";
        }

        // 4. Nếu có mô tả thì ghép thêm mô tả vào sau
        return string.IsNullOrWhiteSpace(desc) ? sentence : $"{sentence} {desc}";
    }

    private static IEnumerable<string> SplitToParts(string text)
    {
        return Regex.Split(text, @"(?<=[\.!\?。！？])\s+").Where(x => !string.IsNullOrWhiteSpace(x));
    }

    private static async Task<Locale?> FindLocaleAsync(string lang, CancellationToken ct)
    {
        var locales = await TextToSpeech.Default.GetLocalesAsync();
        return locales.FirstOrDefault(l => string.Equals(l.Language, lang, StringComparison.OrdinalIgnoreCase))
               ?? locales.FirstOrDefault(l => l.Language.StartsWith(lang.Split('-')[0], StringComparison.OrdinalIgnoreCase));
    }

    private sealed class QueueItem
    {
        public Announcement Announcement { get; }
        public string? OverrideText { get; }
        public CancellationToken ExternalToken { get; }
        public Task Completion => _tcs.Task;
        public bool IsCancelled => ExternalToken.IsCancellationRequested;
        private readonly TaskCompletionSource<bool> _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public QueueItem(Announcement announcement, string? overrideText, CancellationToken externalToken)
        {
            Announcement = announcement;
            OverrideText = overrideText;
            ExternalToken = externalToken;
        }

        public void TrySetResult() => _tcs.TrySetResult(true);
        public void TryCancel() => _tcs.TrySetCanceled();
    }
}