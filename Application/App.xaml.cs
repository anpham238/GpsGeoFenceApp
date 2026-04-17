using MauiApp1.Services.Api;
using MauiApp1.Services.Guest;
using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
        protected override async void OnStart()
        {
            try
            {
                _ = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                _ = await Permissions.RequestAsync<Permissions.LocationAlways>();

                IPlatformApplication.Current?.Services
                    .GetService<GuestHeartbeatService>()?.Start();

                // Nếu đã đăng nhập → chuyển thẳng vào MapPage
                if (AuthApiClient.IsLoggedIn())
                    await Shell.Current.GoToAsync("//map");
                // Nếu chưa → AppShell mặc định sẽ hiển thị LoginPage
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] OnStart: {ex.Message}");
            }
        }

        protected override async void OnSleep()
        {
            try
            {
                var hb = IPlatformApplication.Current?.Services
                    .GetService<GuestHeartbeatService>();
                if (hb is not null) await hb.StopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] OnSleep: {ex.Message}");
            }
        }

        protected override void OnResume()
        {
            try
            {
                IPlatformApplication.Current?.Services
                    .GetService<GuestHeartbeatService>()?.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] OnResume: {ex.Message}");
            }
        }
    }
}