using MapApi.Data;
using MapApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Services;

public sealed class PoiManagementService
{
    private readonly AppDb _db;
    private readonly TranslatorClient _translator;

    // Các ngôn ngữ TTS sẽ tự động dịch và lưu vào PoiLanguage
    // vi-VN luôn là ngôn ngữ gốc, được lưu đầu tiên, không cần dịch
    public static readonly string[] TargetLanguages =
    [
        "en-US",   // Tiếng Anh
        "zh-Hans", // Tiếng Trung (giản thể)
        "ja-JP",   // Tiếng Nhật
        "ko-KR",   // Tiếng Hàn
        "fr-FR",   // Tiếng Pháp
        "de-DE",   // Tiếng Đức
        "es-ES",   // Tiếng Tây Ban Nha
        "th-TH",   // Tiếng Thái
    ];

    public PoiManagementService(AppDb db, TranslatorClient translator)
    {
        _db = db;
        _translator = translator;
    }

    /// <summary>
    /// Thêm/cập nhật POI và tự động dịch sang tất cả ngôn ngữ, lưu vào PoiLanguage.
    /// </summary>
    public async Task AddOrUpdatePoiWithAutoTranslationAsync(
        Poi poi, string viName, string? viDesc, string? viNarration,
        IProgress<string>? progress = null)
    {
        // 1. Upsert vào bảng Pois
        var existing = await _db.Pois.FindAsync(poi.Id);
        if (existing is null)
            _db.Pois.Add(poi);
        else
            _db.Entry(existing).CurrentValues.SetValues(poi);

        await _db.SaveChangesAsync();
        progress?.Report($"[POI] Đã lưu: {viName}");

        // 2. Lưu bản gốc tiếng Việt
        await UpsertLanguageAsync(poi.Id, "vi-VN", viName, viDesc, viNarration);
        progress?.Report($"  → vi-VN ✓");

        // 3. Dịch sang từng ngôn ngữ và lưu
        foreach (var lang in TargetLanguages)
        {
            try
            {
                var tName = await _translator.TryTranslateAsync(viName, lang, "vi-VN") ?? viName;

                var tDesc = string.IsNullOrWhiteSpace(viDesc) ? null
                    : await _translator.TryTranslateAsync(viDesc, lang, "vi-VN");

                var tNar = string.IsNullOrWhiteSpace(viNarration) ? null
                    : await _translator.TryTranslateAsync(viNarration, lang, "vi-VN");

                // Fallback: nếu Azure không dịch được, vẫn lưu tên gốc
                await UpsertLanguageAsync(poi.Id, lang, tName, tDesc, tNar);
                progress?.Report($"  → {lang} ✓");
            }
            catch (Exception ex)
            {
                progress?.Report($"  → {lang} ✗ ({ex.Message})");
            }
        }
    }

    // ── Upsert dùng EF Core (không cần Stored Procedure) ─────────────────
    private async Task UpsertLanguageAsync(
        string idPoi, string langTag, string name, string? desc, string? nar)
    {
        var row = await _db.PoiLanguages
            .FirstOrDefaultAsync(x => x.IdPoi == idPoi && x.LanguageTag == langTag);

        if (row is not null)
        {
            row.NamePoi = name;
            row.Description = desc;
            row.NarTTS = nar;
        }
        else
        {
            _db.PoiLanguages.Add(new PoiLanguage
            {
                IdPoi = idPoi, LanguageTag = langTag,
                NamePoi = name, Description = desc, NarTTS = nar
            });
        }

        await _db.SaveChangesAsync();
    }
}