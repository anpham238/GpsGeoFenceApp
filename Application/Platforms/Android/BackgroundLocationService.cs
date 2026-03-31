#if ANDROID
using Microsoft.Maui;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using MauiApp1.Services;
using System;
namespace MauiApp1.Platforms.Android;

[Service(ForegroundServiceType = ForegroundService.TypeLocation)]
public sealed class BackgroundLocationService : Service
{
    private ILocationService? _locationService;
    private const string ChannelId = "gps_channel";

    public override void OnCreate()
    {
        base.OnCreate();
        try
        {
            // Ép kiểu an toàn với toán tử 'as' và kiểm tra null cho Service Provider
            var sp = IPlatformApplication.Current?.Services;
            _locationService = sp?.GetService(typeof(ILocationService)) as ILocationService;
        }
        catch { _locationService = null; }

        // Nếu DI không có, khởi tạo thủ công (đảm bảo không bao giờ null sau dòng này)
        _locationService ??= new MauiApp1.Services.AndroidLocationService();
        CreateNotificationChannel();
    }


    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        StartForeground(1, CreateNotification());

        if (_locationService is null)
        {
            System.Diagnostics.Debug.WriteLine("[BG] ILocationService is null, skip tracking");
            return StartCommandResult.Sticky;
        }

        _locationService.StartTracking((lat, lng) =>
        {
        System.Diagnostics.Debug.WriteLine($"[BG] {lat}, {lng}");

        });

        return StartCommandResult.Sticky;
    }
    private void CreateNotificationChannel()
    {
        var mgr = (NotificationManager?)GetSystemService(NotificationService);
        if (mgr is null) return;

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O &&
            mgr.GetNotificationChannel(ChannelId) is null)
        {
            var ch = new NotificationChannel(ChannelId, "GPS Tracking", NotificationImportance.Low)
            { Description = "Đang theo dõi vị trí" };
            mgr.CreateNotificationChannel(ch);
        }
    }
    private Notification CreateNotification()
    {

        var builder = new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("GPS Tracking")
            .SetContentText("Đang theo dõi vị trí")
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifyMore) // Sửa lỗi Resource
            .SetOngoing(true);
     var notification = builder.Build();

        // Đảm bảo không trả về null
        return notification ?? throw new InvalidOperationException("Could not create notification");
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        _locationService?.StopTracking();
        base.OnDestroy();
    }
}
#endif