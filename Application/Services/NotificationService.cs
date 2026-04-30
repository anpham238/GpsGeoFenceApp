namespace MauiApp1.Services;

public static class NotificationService
{
    public static void ShowNewPoiNotification(int count)
    {
#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var channelId = "poi_updates";
            var notifManager = (Android.App.NotificationManager?)
                context.GetSystemService(Android.Content.Context.NotificationService);

            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.O)
            {
                var channel = new Android.App.NotificationChannel(
                    channelId, "Cập nhật địa điểm",
                    Android.App.NotificationImportance.Default);
                notifManager?.CreateNotificationChannel(channel);
            }

            var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
            var pendingIntent = Android.App.PendingIntent.GetActivity(
                context, 0, intent,
                Android.App.PendingIntentFlags.UpdateCurrent | Android.App.PendingIntentFlags.Immutable);

            var notification = new AndroidX.Core.App.NotificationCompat.Builder(context, channelId)
                .SetSmallIcon(Resource.Mipmap.appicon)
                .SetContentTitle("Smart Tourism")
                .SetContentText($"📍 Có {count} địa điểm mới! Mở app để khám phá.")
                .SetAutoCancel(true)
                .SetContentIntent(pendingIntent)
                .Build();

            notifManager?.Notify(1001, notification);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] {ex.Message}");
        }
#endif
    }
}
