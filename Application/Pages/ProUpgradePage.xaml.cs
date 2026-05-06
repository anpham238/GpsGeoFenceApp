using MauiApp1.Services.Api;

namespace MauiApp1.Pages;

[QueryProperty(nameof(IsPaywall), "isPaywall")]
public partial class ProUpgradePage : ContentPage
{
    private readonly ProfileApiClient _profileApi;

    public bool IsPaywall { get; set; }

    public ProUpgradePage(ProfileApiClient profileApi)
    {
        InitializeComponent();
        _profileApi = profileApi;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (IsPaywall)
            PaywallBanner.IsVisible = true;

        if (AuthApiClient.IsPro())
        {
            BtnUpgradePro.Text            = "Đã là PRO ✅";
            BtnUpgradePro.BackgroundColor = Color.FromArgb("#78909C");
            BtnUpgradePro.IsEnabled       = false;
            FreeStatusLabel.Text          = "PRO – Không giới hạn";
            FreeStatusLabel.TextColor     = Color.FromArgb("#FFD700");
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("..");

    private async void OnChooseAreaClicked(object sender, EventArgs e)
    {
        if (!AuthApiClient.IsLoggedIn())
        {
            await DisplayAlert("Cần đăng nhập", "Vui lòng đăng nhập để mua Area Pack.", "OK");
            return;
        }
        await Shell.Current.GoToAsync("areapackselect");
    }

    private async void OnUpgradeProClicked(object sender, EventArgs e)
    {
        if (!AuthApiClient.IsLoggedIn())
        {
            await DisplayAlert("Cần đăng nhập", "Vui lòng đăng nhập để nâng cấp.", "OK");
            return;
        }

        await Shell.Current.GoToAsync("payment", new Dictionary<string, object>
        {
            [nameof(PaymentPage.ProductCode)]   = "PRO_30D",
            [nameof(PaymentPage.ProductName)]   = "Pro Pack",
            [nameof(PaymentPage.AreaName)]      = "",
            [nameof(PaymentPage.Price)]         = "199.000đ",
            [nameof(PaymentPage.DurationLabel)] = "30 ngày"
        });
    }
}
