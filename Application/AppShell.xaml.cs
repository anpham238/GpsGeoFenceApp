using MauiApp1.Pages;
using MauiApp1.Services.Api;

namespace MauiApp1;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("register",      typeof(RegisterPage));
        Routing.RegisterRoute("qrscan",        typeof(Pages.QrScanPage));
        Routing.RegisterRoute("proupgrade",    typeof(ProUpgradePage));
        Routing.RegisterRoute("travelhistory", typeof(TravelHistoryPage));

        // ProfilePage được mở như modal (GoToAsync("profile")) từ avatar trên MapPage
        Routing.RegisterRoute("profile", typeof(ProfilePage));
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        var name = AuthApiClient.GetCurrentUsername();
        if (string.IsNullOrEmpty(name))
            name = Preferences.Get("Username", "");

        var email = AuthApiClient.GetCurrentMail();
        if (string.IsNullOrEmpty(email))
            email = Preferences.Get("Email", "");

        var planType = AuthApiClient.GetCurrentPlanType();

        if (!string.IsNullOrEmpty(name))
        {
            MenuUserName.Text  = name;
            MenuUserEmail.Text = string.IsNullOrEmpty(email) ? "Thành viên Smart Tourism" : email;
            MenuPlanBadge.Text = planType == "PRO" ? "🌟 Gói PRO" : "";
        }
        else
        {
            MenuUserName.Text  = "Khách vãng lai";
            MenuUserEmail.Text = "Bạn chưa đăng nhập";
            MenuPlanBadge.Text = "";
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        AuthApiClient.ClearSession();
        Preferences.Remove("Username");
        Preferences.Remove("Email");

        Shell.Current.FlyoutIsPresented = false;

        MenuUserName.Text  = "Khách du lịch";
        MenuUserEmail.Text = "Bạn chưa đăng nhập";
        MenuPlanBadge.Text = "";

        await Shell.Current.GoToAsync("//map");
    }
}
