using MapApi.Data;
using MapApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Services;

public sealed class PoiManagementService
{
    private readonly AppDb _db;
    private readonly TranslatorClient _translator;

    public static readonly string[] TargetLanguages =
    [
        "en-US",   // Tiếng Anh
        "zh-Hans", // Tiếng Trung (giản thể)
        "ja-JP",   // Tiếng Nhật
        "ko-KR",   // Tiếng Hàn
        "de-DE",   // Tiếng Đức
    ];

    public PoiManagementService(AppDb db, TranslatorClient translator)
    {
        _db = db;
        _translator = translator;
    }

    /// <summary>
    /// Thêm/cập nhật POI và tự động dịch sang tất cả ngôn ngữ, lưu vào PoiLanguage.
    /// TextToSpeech = NarTTS_translated + ". " + Description_translated (kết hợp)
    /// </summary>
    public async Task AddOrUpdatePoiWithAutoTranslationAsync(
        Poi poi, string? viNarration, string? viDesc,
        IProgress<string>? progress = null)
    {
        // 1. Upsert vào bảng Pois
        var existing = await _db.Pois.FindAsync(poi.Id);
        if (existing is null)
            _db.Pois.Add(poi);
        else
            _db.Entry(existing).CurrentValues.SetValues(poi);

        await _db.SaveChangesAsync();
        progress?.Report($"[POI] Đã lưu: {poi.Name} (Id={poi.Id})");

        // 2. Lưu bản gốc tiếng Việt
        var viTts = CombineTts(viNarration, viDesc);
        await UpsertLanguageAsync(poi.Id, "vi-VN", viTts);
        progress?.Report("  → vi-VN ✓");

        // 3. Dịch sang từng ngôn ngữ và lưu
        foreach (var lang in TargetLanguages)
        {
            try
            {
                var tNar = string.IsNullOrWhiteSpace(viNarration) ? null
                    : await _translator.TryTranslateAsync(viNarration, lang, "vi-VN");

                var tDesc = string.IsNullOrWhiteSpace(viDesc) ? null
                    : await _translator.TryTranslateAsync(viDesc, lang, "vi-VN");

                var tts = CombineTts(tNar, tDesc);
                await UpsertLanguageAsync(poi.Id, lang, tts);
                progress?.Report($"  → {lang} ✓");
            }
            catch (Exception ex)
            {
                progress?.Report($"  → {lang} ✗ ({ex.Message})");
            }
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string? CombineTts(string? nar, string? desc)
    {
        var parts = new[] { nar?.Trim(), desc?.Trim() }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var combined = string.Join(". ", parts);
        return string.IsNullOrWhiteSpace(combined) ? null : combined;
    }

    private async Task UpsertLanguageAsync(int idPoi, string langTag, string? tts)
    {
        var row = await _db.PoiLanguages
            .FirstOrDefaultAsync(x => x.IdPoi == idPoi && x.LanguageTag == langTag);

        if (row is not null)
        {
            row.TextToSpeech = tts;
        }
        else
        {
            _db.PoiLanguages.Add(new PoiLanguage
            {
                IdPoi = idPoi,
                LanguageTag = langTag,
                TextToSpeech = tts
            });
        }

        await _db.SaveChangesAsync();
    }
}
