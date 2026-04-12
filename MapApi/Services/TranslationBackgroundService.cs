using MapApi.Data;
using MapApi.Services;
using Microsoft.EntityFrameworkCore;

namespace MapApi.Services;

public sealed class TranslationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TranslationBackgroundService> _logger;
    private readonly IConfiguration _config;

    public TranslationBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<TranslationBackgroundService> logger,
        IConfiguration config)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
        _config       = config;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Delay nhỏ để DB / EF sẵn sàng trước
        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        await RunTranslationAsync(ct);

        var minutes  = _config.GetValue<int>("Translation:BackgroundIntervalMinutes", 10);
        var interval = TimeSpan.FromMinutes(minutes);

        using var timer = new PeriodicTimer(interval);
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
            await RunTranslationAsync(ct);
    }

    private async Task RunTranslationAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db  = scope.ServiceProvider.GetRequiredService<AppDb>();
            var svc = scope.ServiceProvider.GetRequiredService<PoiManagementService>();

            var totalLangs = 1 + PoiManagementService.TargetLanguages.Length; // vi-VN + 4

            // POI chưa có đủ bản dịch
            var allPoiIds = await db.Pois.AsNoTracking()
                .Where(p => p.IsActive)
                .Select(p => p.Id)
                .ToListAsync(ct);

            var translatedIds = await db.PoiLanguages.AsNoTracking()
                .GroupBy(x => x.IdPoi)
                .Where(g => g.Count() >= totalLangs)
                .Select(g => g.Key)
                .ToListAsync(ct);

            var missing = allPoiIds.Except(translatedIds).ToList();
            if (missing.Count == 0)
            {
                _logger.LogDebug("Auto-translate: tất cả POI đã có đủ bản dịch.");
                return;
            }

            _logger.LogInformation("Auto-translate: {Count} POI cần dịch.", missing.Count);

            foreach (var id in missing)
            {
                if (ct.IsCancellationRequested) break;

                var poi = await db.Pois.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == id, ct);
                if (poi is null) continue;

                var viRow = await db.PoiLanguages.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.IdPoi == id && x.LanguageTag == "vi-VN", ct);

                await svc.AddOrUpdatePoiWithAutoTranslationAsync(
                    poi,
                    viRow?.NamePoi ?? poi.Name,
                    viRow?.Description ?? poi.Description,
                    viRow?.NarTTS,
                    progress: null);

                _logger.LogInformation("Auto-translated: {Id}", id);
            }

            _logger.LogInformation("Auto-translate: hoàn tất {Count} POI.", missing.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Auto-translate lỗi.");
        }
    }
}
