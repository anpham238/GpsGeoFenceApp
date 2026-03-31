#if ANDROID
using System;
using Android.App;
using Android.Content;
using Android.Gms.Location;
using Android.OS;
using AndroidX.Core.App;

namespace MauiApp1.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { "com.google.android.location.GEOFENCE_TRANSITION" })]
public sealed class GeofenceBroadcastReceiver : BroadcastReceiver
{
    // Cập nhật tham số Context? và Intent? để khớp với định nghĩa của Android SDK
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var ev = GeofencingEvent.FromIntent(intent);
        if (ev == null || ev.HasError) return;

        var geofences = ev.TriggeringGeofences;
        if (geofences == null) return;

        foreach (var gf in geofences)
        {
            if (gf != null)
                GeofenceEventHub.Raise(gf.RequestId, ev.GeofenceTransition);
        }

        // Test: ShowNotification(context, $"Geofence: {ev.GeofenceTransition}");
    }

    static void ShowNotification(Context ctx, string text)
    {
        const string channelId = "geo_channel";
        var mgr = (NotificationManager?)ctx.GetSystemService(Context.NotificationService);

        if (mgr == null) return; // Bảo vệ nếu không lấy được NotificationManager

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            if (mgr.GetNotificationChannel(channelId) == null)
            {
                mgr.CreateNotificationChannel(new NotificationChannel(channelId, "Geofence", NotificationImportance.Default));
            }
        }

        var notif = new NotificationCompat.Builder(ctx, channelId)
            .SetContentTitle("POI event")
            .SetContentText(text)
            .SetSmallIcon(global::Android.Resource.Drawable.StatNotifyMore) // Dùng global:: để tránh nhầm lẫn namespace
            .Build();

        mgr.Notify(new Random().Next(), notif);
    }
}

internal static class GeofenceEventHub
{
    public static event Action<string, int>? OnTransition;
    public static void Raise(string poiId, int transition) => OnTransition?.Invoke(poiId, transition);
}
#endif