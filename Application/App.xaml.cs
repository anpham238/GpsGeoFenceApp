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
            _ = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            _ = await Permissions.RequestAsync<Permissions.LocationAlways>();
        }
    }
}