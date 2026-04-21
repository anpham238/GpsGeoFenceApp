using MauiApp1.Services.Api;

namespace MauiApp1.Pages;

public partial class ProUpgradePage : ContentPage
{
    private readonly ProfileApiClient _profileApi;

    public ProUpgradePage(ProfileApiClient profileApi)
    {
        InitializeComponent();
        _profileApi = profileApi;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (AuthApiClient.IsPro())
        {
            BtnUpgrade.Text            = "Đã là PRO";
            BtnUpgrade.BackgroundColor = Color.FromArgb("#78909C");
            BtnUpgrade.IsEnabled       = false;
        }
    }

    private async void OnUpgradeNowClicked(object sender, EventArgs e)
    {
        if (!AuthApiClient.IsLoggedIn())
        {
            await DisplayAlertAsync("Thông báo", "Vui lòng đăng nhập để nâng cấp.", "OK");
            return;
        }

        BtnUpgrade.IsEnabled = false;
        BtnUpgrade.Text      = "Đang xử lý...";

        var success = await _profileApi.UpgradeToProAsync();

        if (success)
        {
            BtnUpgrade.Text            = "Đã là PRO ✅";
            BtnUpgrade.BackgroundColor = Color.FromArgb("#4CAF50");
            await DisplayAlertAsync("Chúc mừng! 🌟", "Bạn đã nâng cấp thành công lên Gói PRO.\nTận hưởng trải nghiệm không giới hạn!", "Tuyệt vời!");
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            BtnUpgrade.IsEnabled = true;
            BtnUpgrade.Text      = "Nâng Cấp Ngay";
            await DisplayAlertAsync("Lỗi", "Không thể nâng cấp lúc này. Vui lòng thử lại.", "OK");
        }
    }
}
