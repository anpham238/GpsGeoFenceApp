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
            .ConfigureFonts(fonts =>
            {
                // ✅ Đăng ký alias font (nếu XAML dùng OpenSansRegular)
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

        // ── API clients ───────────────────────────────────────────────
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

        // ── Sync engine ───────────────────────────────────────────────
        builder.Services.AddSingleton<PoiSyncService>();

        // ── Pages ─────────────────────────────────────────────────────
        builder.Services.AddSingleton<MapPage>();

        var app = builder.Build();

        // Init + AutoSync
        _ = Task.Run(async () =>
        {
            try
            {
                var db = app.Services.GetRequiredService<PoiDatabase>();
                await db.InitAsync();

                // đợi nhẹ cho MapApi kịp chạy (dev)
                await Task.Delay(1500);

                var sync = app.Services.GetRequiredService<PoiSyncService>();
                await sync.SyncOnceAsync();
                sync.StartAutoSync(TimeSpan.FromMinutes(2));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Startup] Init/Sync error: {ex}");
            }
        });

        return app;
    }
}
