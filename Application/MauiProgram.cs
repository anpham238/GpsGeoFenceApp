using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Maps;

using MauiApp1.Data;
using MauiApp1.Pages;
using MauiApp1.Platforms.Android.Services;
using MauiApp1.Services;
using MauiApp1.Services.Api;
using MauiApp1.Services.Audio;
using MauiApp1.Services.Narration;
using MauiApp1.Services.Sync;

using ZXing.Net.Maui.Controls;

namespace MauiApp1;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .UseMauiCommunityToolkit()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // ── GPS & Geofence ────────────────────────────────────────────
#if ANDROID
        builder.Services.AddSingleton<ILocationService, AndroidLocationService>();
        builder.Services.AddSingleton<IGeofenceService, AndroidGeofenceService>();
        builder.Services.AddSingleton<IAudioPlayer, AndroidAudioPlayer>();
#else
        builder.Services.AddSingleton<ILocationService, NoopLocationService>();
        builder.Services.AddSingleton<IGeofenceService, NoopGeofenceService>();
        builder.Services.AddSingleton<IAudioPlayer, NoopAudioPlayer>();
#endif

        // ── Audio & Narration ─────────────────────────────────────────
        builder.Services.AddSingleton<AudioCache>();
        builder.Services.AddSingleton<NarrationManager>();

        // ── Local DB (SQLite) ─────────────────────────────────────────
        builder.Services.AddSingleton<PoiDatabase>();
        builder.Services.AddSingleton<SyncMetadataRepository>();
        builder.Services.AddSingleton<PoiNarrationCache>();

        // ── API clients (emulator: 10.0.2.2) ─────────────────────────
        builder.Services.AddHttpClient<PoiApiClient>(http =>
        {
#if ANDROID
            http.BaseAddress = new Uri("http://10.0.2.2:5150");
#else
            http.BaseAddress = new Uri("http://localhost:5150");
#endif
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddHttpClient<PlaybackApiClient>(http =>
        {
#if ANDROID
            http.BaseAddress = new Uri("http://10.0.2.2:5150");
#else
            http.BaseAddress = new Uri("http://localhost:5150");
#endif
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddHttpClient<PoiNarrationApiClient>(http =>
        {
#if ANDROID
            http.BaseAddress = new Uri("http://10.0.2.2:5150");
#else
            http.BaseAddress = new Uri("http://localhost:5150");
#endif
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        // ✅ THÊM: TranslatorClient (Azure Translator)
        builder.Services.AddHttpClient<TranslatorClient>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
        });

        // ── Sync engine ───────────────────────────────────────────────
        builder.Services.AddSingleton<PoiSyncService>();

        // ── Pages ─────────────────────────────────────────────────────
        // ✅ Khuyến nghị: MapPage nên Transient để tránh giữ page sống quá lâu
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<QrScanPage>();

        return builder.Build();
    }
}