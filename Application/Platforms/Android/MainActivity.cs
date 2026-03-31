using Android.App;
using Android.Content.PM;
using Android.OS;

namespace MauiApp1
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {

        protected override void OnCreate(Bundle? savedInstanceState) // Thêm dấu ? ở đây
        {
            base.OnCreate(savedInstanceState);
            // Init model model của Platform cũng cần truyền savedInstanceState (có thể null)
            Microsoft.Maui.ApplicationModel.Platform.Init(this, savedInstanceState);
        }

    }
}
