using Microsoft.Maui.Controls.Maps;
using MauiApp1.Pages;
using MauiApp1.Services;
using MauiApp1.Data;

namespace MauiApp1;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiMaps(); // Map control
#if ANDROID
        builder.Services.AddSingleton<ILocationService, AndroidLocationService>();
        builder.Services.AddSingleton<IGeofenceService, AndroidGeofenceService>();
        builder.Services.AddSingleton<PoiDbService>();
#else
builder.Services.AddSingleton<ILocationService, NoopLocationService>();
builder.Services.AddSingleton<IGeofenceService, NoopGeofenceService>();
#endif
        builder.Services.AddSingleton<MapPage>();
        return builder.Build();
    }
}