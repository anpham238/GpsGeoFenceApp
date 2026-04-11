using MapApi.Data;
using MapApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Services;

public sealed class PoiManagementService
{
    private readonly AppDb _db;
    private readonly TranslatorClient _translator;

    // Danh sách các ngôn ngữ muốn tự động dịch ra
    private readonly string[] _targetLanguages = { "en-US", "ja-JP", "ko-KR", "de-DE" };

    public PoiManagementService(AppDb db, TranslatorClient translator)
    {
        _db = db;
        _translator = translator;
    }
    public async Task AddOrUpdatePoiWithAutoTranslationAsync(Poi poi, string viName, string? viDesc, string? viNarration)
    {
        // 1. Lưu thông tin gốc vào bảng Pois
        var existingPoi = await _db.Pois.FindAsync(poi.Id);
        if (existingPoi == null)
        {
            _db.Pois.Add(poi);
        }
        else
        {
            _db.Entry(existingPoi).CurrentValues.SetValues(poi);
        }
        await _db.SaveChangesAsync(); // Lưu bảng gốc trước để có IdPoi cho khóa ngoại

        // 2. Lưu bản gốc Tiếng Việt (vi-VN) vào PoiLanguage
        await UpsertLanguageAsync(poi.Id, "vi-VN", viName, viDesc, viNarration);

        // 3. Chạy vòng lặp tự động dịch sang các ngôn ngữ khác và lưu
        foreach (var lang in _targetLanguages)
        {
            // Dịch Tên
            var translatedName = await _translator.TryTranslateAsync(viName, lang, "vi-VN") ?? viName;

            // Dịch Mô tả
            var translatedDesc = string.IsNullOrWhiteSpace(viDesc)
                ? null
                : await _translator.TryTranslateAsync(viDesc, lang, "vi-VN");

            // Dịch Thuyết minh (Narration)
            var translatedNarration = string.IsNullOrWhiteSpace(viNarration)
                ? null
                : await _translator.TryTranslateAsync(viNarration, lang, "vi-VN");

            // Gọi hàm lưu vào Database
            await UpsertLanguageAsync(poi.Id, lang, translatedName, translatedDesc, translatedNarration);
        }
    }

    // Helper gọi Stored Procedure để Upsert
    private async Task UpsertLanguageAsync(string idPoi, string langTag, string name, string? desc, string? nar)
    {
        var sql = "EXEC dbo.UpsertPoiLanguage @IdPoi={0}, @LanguageTag={1}, @NamePoi={2}, @NarTTS={3}, @Description={4}";
        await _db.Database.ExecuteSqlRawAsync(sql, idPoi, langTag, name, nar, desc);
    }
}