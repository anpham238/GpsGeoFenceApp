#if ANDROID
using Android.App;
using Android.Content;
using Android.Telephony;
using MauiApp1.Services.Narration;

namespace MauiApp1.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(new[] { "android.intent.action.PHONE_STATE" })]
public sealed class CallStateReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        var state = intent?.GetStringExtra(TelephonyManager.ExtraState);
        if (state == TelephonyManager.ExtraStateRinging
         || state == TelephonyManager.ExtraStateOffhook)
        {
            var narration = IPlatformApplication.Current?.Services
                                .GetService<NarrationManager>();
            narration?.Stop();
            System.Diagnostics.Debug.WriteLine("[CallReceiver] Cuộc gọi → dừng narration");
        }
    }
}
#endif
