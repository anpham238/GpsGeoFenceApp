using MauiApp1.Services.Api;

namespace MauiApp1.Pages;

[QueryProperty(nameof(PackageDisplay), nameof(PackageDisplay))]
[QueryProperty(nameof(ExpiryDisplay),  nameof(ExpiryDisplay))]
public partial class PaymentSuccessPage : ContentPage
{
    public string PackageDisplay { get; set; } = "";
    public string ExpiryDisplay  { get; set; } = "";

    public PaymentSuccessPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LblPackage.Text = PackageDisplay;

        if (!string.IsNullOrWhiteSpace(ExpiryDisplay))
            LblExpiry.Text = ExpiryDisplay;
        else
        {
            ExpiryRow.IsVisible = false;
        }
    }

    private async void OnExploreClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//map");

    private async void OnHomeClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync("//map");
}
