using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace MauiApp1
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                               ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    [IntentFilter(
        new[] { Intent.ActionView },
        Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
        DataScheme = "smarttourism",
        DataHost = "poi")]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Microsoft.Maui.ApplicationModel.Platform.Init(this, savedInstanceState);
            HandleDeepLink(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleDeepLink(intent);
        }

        private static void HandleDeepLink(Intent? intent)
        {
            if (intent?.Action != Intent.ActionView) return;
            var raw = intent.Data?.ToString();
            if (string.IsNullOrEmpty(raw)) return;
            DeepLinkHandler.PendingUri = raw;
        }
    }

    public static class DeepLinkHandler
    {
        public static string? PendingUri { get; set; }

        public static (int PoiId, string Lang)? Parse(string uri)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var u)) return null;
            if (!u.Scheme.Equals("smarttourism", StringComparison.OrdinalIgnoreCase)) return null;

            var path = u.AbsolutePath.Trim('/');
            if (!int.TryParse(path.Split('/')[0], out var poiId)) return null;

            var lang = "vi-VN";
            foreach (var part in u.Query.TrimStart('?').Split('&'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && kv[0] == "lang")
                    lang = Uri.UnescapeDataString(kv[1]);
            }
            return (poiId, lang);
        }
    }
}
