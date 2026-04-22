#if ANDROID
using Android.Content;
using Android.OS;
using AndroidX.Core.Content;
using MauiApp1.Services;
namespace MauiApp1.Platforms.Android.Services;
public sealed class AndroidBackgroundLocationRuntime : IBackgroundLocationRuntime
{
    private readonly Context _context = global::Android.App.Application.Context;

    public Task StartAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

        var intent = new Intent(_context, typeof(MauiApp1.Platforms.Android.BackgroundLocationService));
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            ContextCompat.StartForegroundService(_context, intent);
        else
            _context.StartService(intent);

        System.Diagnostics.Debug.WriteLine("[BG] BackgroundLocationService started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return Task.FromCanceled(ct);

        var intent = new Intent(_context, typeof(MauiApp1.Platforms.Android.BackgroundLocationService));
        _context.StopService(intent);
        System.Diagnostics.Debug.WriteLine("[BG] BackgroundLocationService stopped");
        return Task.CompletedTask;
    }
}
#endif
