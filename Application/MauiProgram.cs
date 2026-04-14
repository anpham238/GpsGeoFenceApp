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
        string apiBaseUrl = "http://192.168.1.121:5150";
        // ── API clients ─────────────────────────
        builder.Services.AddHttpClient<PoiApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(60);
        });
        builder.Services.AddHttpClient<PlaybackApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddHttpClient<PoiNarrationApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(30);
        });
        // Thêm vào sau PoiNarrationApiClient registration:
        builder.Services.AddHttpClient<AuthApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        // ✅ THÊM: TranslatorClient (Azure Translator)
        builder.Services.AddHttpClient<TranslatorClient>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddHttpClient<TourApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(30);
        });
        builder.Services.AddHttpClient<AnalyticsClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(10);
        });
        // ── Sync engine ───────────────────────────────────────────────
        builder.Services.AddSingleton<PoiSyncService>();
        // ── Pages ─────────────────────────────────────────────────────
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<QrScanPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        return builder.Build();
    }
}