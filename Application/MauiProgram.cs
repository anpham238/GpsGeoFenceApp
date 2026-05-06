using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Maps;
using MauiApp1.Data;
using MauiApp1.Pages;
using MauiApp1.Platforms.Android.Services;
using MauiApp1.Services;
using MauiApp1.Services.Api;
using MauiApp1.Services.Audio;
using MauiApp1.Services.Guest;
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
        builder.Services.AddSingleton<IBackgroundLocationRuntime, AndroidBackgroundLocationRuntime>();
        builder.Services.AddSingleton<IAudioPlayer, AndroidAudioPlayer>();
#else
        builder.Services.AddSingleton<ILocationService, NoopLocationService>();
        builder.Services.AddSingleton<IGeofenceService, NoopGeofenceService>();
        builder.Services.AddSingleton<IBackgroundLocationRuntime, NoopBackgroundLocationRuntime>();
        builder.Services.AddSingleton<IAudioPlayer, NoopAudioPlayer>();
#endif
        // ── Audio & Narration ─────────────────────────────────────────
        builder.Services.AddSingleton<AudioCache>();
        builder.Services.AddSingleton<NarrationManager>();
        builder.Services.AddSingleton<PoiNarrationHandler>();
        builder.Services.AddSingleton<PoiDatabase>();
        builder.Services.AddSingleton<SyncMetadataRepository>();
        builder.Services.AddSingleton<PoiNarrationCache>();

        // Ưu tiên:
        //  1. Preferences (user hoặc Settings page đã lưu)
        //  2. server_url.txt được AppBuildService bake vào APK lúc build
        //  3. Fallback hardcode (Dev Tunnels / LAN)
        const string FallbackApiUrl = "https://95sccqzq-7286.asse.devtunnels.ms/";
        var embeddedUrl     = TryReadEmbeddedServerUrl();
        var defaultApiBaseUrl = embeddedUrl ?? FallbackApiUrl;
        var rawApiBaseUrl = Preferences.Default.Get("ApiBaseUrl", defaultApiBaseUrl);
        var apiBaseUrl = NormalizeApiBaseUrl(rawApiBaseUrl, defaultApiBaseUrl);

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
        builder.Services.AddHttpClient<TicketApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddHttpClient<GuestDeviceApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddHttpClient<ProfileApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(15);
        });
        builder.Services.AddHttpClient<UsageApiClient>(http =>
        {
            http.BaseAddress = new Uri(apiBaseUrl);
            http.Timeout = TimeSpan.FromSeconds(10);
        });
        // ── Guest tracking (ẩn danh) ──────────────────────────────────
        builder.Services.AddSingleton<GuestDeviceService>();
        builder.Services.AddSingleton<GuestHeartbeatService>();
        // ── Sync engine ───────────────────────────────────────────────
        builder.Services.AddSingleton<PoiSyncService>();
        // ── Pages ─────────────────────────────────────────────────────
        builder.Services.AddTransient<MapPage>();
        builder.Services.AddTransient<QrScanPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<RegisterPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<ProUpgradePage>();
        builder.Services.AddTransient<AreaPackSelectPage>();
        builder.Services.AddTransient<PaymentPage>();
        builder.Services.AddTransient<PaymentSuccessPage>();
        builder.Services.AddTransient<TravelHistoryPage>();
        builder.Services.AddTransient<VisitedHistoryPage>();
        return builder.Build();
    }
    /// <summary>
    /// Đọc URL server được AppBuildService ghi vào Resources/Raw/server_url.txt trước khi build.
    /// Trả về null nếu file rỗng hoặc không tồn tại (Debug từ VS sẽ dùng Preferences / fallback).
    /// </summary>
    private static string? TryReadEmbeddedServerUrl()
    {
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync("server_url.txt").GetAwaiter().GetResult();
            using var reader = new StreamReader(stream);
            var url = reader.ReadToEnd().Trim();
            return string.IsNullOrWhiteSpace(url) ? null : url;
        }
        catch { return null; }
    }

    private static string NormalizeApiBaseUrl(string? rawApiBaseUrl, string fallbackApiBaseUrl)
    {
        var apiBaseUrl = string.IsNullOrWhiteSpace(rawApiBaseUrl)
            ? fallbackApiBaseUrl
            : rawApiBaseUrl.Trim();

        if (!apiBaseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !apiBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            apiBaseUrl = "http://" + apiBaseUrl;
        }
#if ANDROID
        if (Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var parsed) &&
            (string.Equals(parsed.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
             parsed.Host == "127.0.0.1"))
        {
            var port = parsed.IsDefaultPort ? 5150 : parsed.Port;
            var scheme = string.IsNullOrWhiteSpace(parsed.Scheme) ? "http" : parsed.Scheme;
            apiBaseUrl = $"{scheme}://10.0.2.2:{port}";
        }
#endif
        return apiBaseUrl;
    }
}