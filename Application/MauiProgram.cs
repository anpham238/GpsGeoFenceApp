using CommunityToolkit.Maui;
using MauiApp1.Data;
using MauiApp1.Pages;
using MauiApp1.Platforms.Android.Services;
using MauiApp1.Services;
using MauiApp1.Services.Audio;
using MauiApp1.Services.Narration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Maps;

namespace MauiApp1;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiMaps()
            .UseMauiCommunityToolkit();

        // ── GPS & Geofence ────────────────────────────────────────────────
#if ANDROID
        builder.Services.AddSingleton<ILocationService, AndroidLocationService>();
        builder.Services.AddSingleton<IGeofenceService, AndroidGeofenceService>();
        builder.Services.AddSingleton<IAudioPlayer, AndroidAudioPlayer>();
#else
        builder.Services.AddSingleton<ILocationService, NoopLocationService>();
        builder.Services.AddSingleton<IGeofenceService, NoopGeofenceService>();
        builder.Services.AddSingleton<IAudioPlayer, NoopAudioPlayer>();
#endif

        // ── Audio & Narration ─────────────────────────────────────────────
        builder.Services.AddSingleton<AudioCache>();
        builder.Services.AddSingleton<NarrationManager>();

        // ── Database + API Sync ───────────────────────────────────────────
        builder.Services.AddSingleton<PoiDatabase>();
        builder.Services.AddSingleton<ApiService>();   // goi API, sync SQLite

        // ── Pages ─────────────────────────────────────────────────────────
        builder.Services.AddSingleton<MapPage>();
        builder.Logging.AddDebug();

        // Khoi tao SQLite va sync POI tu API (khong block startup)
        var app = builder.Build();

        // 1) Init SQLite (tao bang, seed local neu trong)
        app.Services.GetRequiredService<PoiDatabase>()
            .InitAsync()
            .GetAwaiter().GetResult();

        // 2) Sync POI tu SQL Server (chay nen, khong can cho)
        _ = Task.Run(async () =>
        {
            var api = app.Services.GetRequiredService<ApiService>();
            await api.SyncPoisAsync();
        });

        return app;
    }
}