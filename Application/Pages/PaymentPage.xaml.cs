using MauiApp1.Services.Api;

namespace MauiApp1.Pages;

[QueryProperty(nameof(ProductCode),   nameof(ProductCode))]
[QueryProperty(nameof(ProductName),   nameof(ProductName))]
[QueryProperty(nameof(AreaName),      nameof(AreaName))]
[QueryProperty(nameof(Price),         nameof(Price))]
[QueryProperty(nameof(DurationLabel), nameof(DurationLabel))]
public partial class PaymentPage : ContentPage
{
    private readonly ProfileApiClient _profileApi;
    private string _selectedPaymentMethod = "WALLET";

    public string ProductCode   { get; set; } = "";
    public string ProductName   { get; set; } = "";
    public string AreaName      { get; set; } = "";
    public string Price         { get; set; } = "";
    public string DurationLabel { get; set; } = "";

    public PaymentPage(ProfileApiClient profileApi)
    {
        InitializeComponent();
        _profileApi = profileApi;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        LblProductName.Text = ProductName;
        LblPrice.Text       = Price;
        LblDuration.Text    = DurationLabel;

        bool isAreaPack = !string.IsNullOrWhiteSpace(AreaName);
        LblAreaLabel.IsVisible = isAreaPack;
        LblAreaName.IsVisible  = isAreaPack;
        if (isAreaPack)
            LblAreaName.Text = AreaName;

        bool isPro = ProductCode.StartsWith("PRO", StringComparison.OrdinalIgnoreCase);
        ProBenefitsPanel.IsVisible = isPro;

        // Màu nút theo loại gói
        BtnConfirm.BackgroundColor = isPro
            ? Color.FromArgb("#FFD700")
            : Color.FromArgb("#29B6F6");
        BtnConfirm.TextColor = Color.FromArgb("#0D1B2A");
    }

    private void OnPaymentMethodChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return;
        if (sender == RadioWallet) _selectedPaymentMethod = "WALLET";
        else if (sender == RadioBank) _selectedPaymentMethod = "BANK";
        else if (sender == RadioCode) _selectedPaymentMethod = "CODE";
    }

    private async void OnConfirmPaymentClicked(object sender, EventArgs e)
    {
        BtnConfirm.IsEnabled = false;
        BtnConfirm.Text      = "Đang xử lý...";

        var result = await _profileApi.BuyPackAsync(ProductCode, _selectedPaymentMethod);

        if (result is { Success: true })
        {
            var expiry = result.ExpiresAt.HasValue
                ? result.ExpiresAt.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
                : "";

            var displayName = string.IsNullOrWhiteSpace(AreaName)
                ? ProductName
                : $"{ProductName} - {AreaName}";

            await Shell.Current.GoToAsync("paymentsuccess", new Dictionary<string, object>
            {
                [nameof(PaymentSuccessPage.PackageDisplay)] = displayName,
                [nameof(PaymentSuccessPage.ExpiryDisplay)]  = expiry
            });
        }
        else
        {
            BtnConfirm.IsEnabled = true;
            BtnConfirm.Text      = "XÁC NHẬN THANH TOÁN";
            await DisplayAlert("Lỗi thanh toán", "Không thể hoàn tất giao dịch. Vui lòng thử lại.", "OK");
        }
    }
}
