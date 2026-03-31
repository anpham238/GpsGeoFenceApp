using CommunityToolkit.Maui;
using MauiApp1.Data;
using MauiApp1.Pages;
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

        // ── Database ──────────────────────────────────────────────────────
        builder.Services.AddSingleton<PoiDatabase>();

        // ── Pages ─────────────────────────────────────────────────────────
        builder.Services.AddSingleton<MapPage>();
        builder.Logging.AddDebug();

        // Khoi tao DB + seed lan dau
        var app = builder.Build();
        app.Services.GetRequiredService<PoiDatabase>()
            .InitAsync()
            .GetAwaiter().GetResult();

        return app;
    }
}