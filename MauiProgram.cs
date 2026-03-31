using CommunityToolkit.Maui;
using MauiApp1.Data;
using MauiApp1.Pages;
using MauiApp1.Services;
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

#if ANDROID
        builder.Services.AddSingleton<ILocationService, AndroidLocationService>();
        builder.Services.AddSingleton<IGeofenceService, AndroidGeofenceService>();
#else
        builder.Services.AddSingleton<ILocationService, NoopLocationService>();
        builder.Services.AddSingleton<IGeofenceService, NoopGeofenceService>();
#endif

        // SQLite
        builder.Services.AddSingleton<PoiDatabase>();

        // Pages
        builder.Services.AddSingleton<MapPage>();

        builder.Logging.AddDebug();

        // 🔧 Khởi tạo DB + seed 1 lần khi app khởi động
        var app = builder.Build();
        app.Services.GetRequiredService<PoiDatabase>()
            .InitAsync()
            .GetAwaiter().GetResult();

        return app;
    }
}