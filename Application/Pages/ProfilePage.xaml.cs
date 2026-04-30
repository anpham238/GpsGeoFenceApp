using MauiApp1.Services.Api;
using Microsoft.Maui.Storage;

namespace MauiApp1.Pages;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileApiClient _profileApi;
    private Stream? _avatarStream;
    private string? _avatarFileName;

    public ProfilePage(ProfileApiClient profileApi)
    {
        InitializeComponent();
        _profileApi = profileApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProfileAsync();
        await LoadHistoryCountAsync();
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//map");
    }

    private async Task LoadProfileAsync()
    {
        var profile = await _profileApi.GetMeAsync();

        if (profile is null)
        {
            var name = AuthApiClient.GetCurrentUsername();
            LblGreeting.Text = string.IsNullOrWhiteSpace(name) ? "Xin chào!" : $"Xin chào, {name}!";
            LblEmail.Text = AuthApiClient.GetCurrentMail() ?? "";
            UpdatePlanBadge(AuthApiClient.GetCurrentPlanType());
            return;
        }

        LblGreeting.Text = $"Xin chào, {profile.Username}!";
        LblEmail.Text = profile.Mail ?? "";
        UpdatePlanBadge(profile.PlanType);

        var baseUrl = _profileApi.GetBaseUrl().TrimEnd('/');
        var avatarUrl = profile.AvatarUrl;
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            AvatarImage.Source = avatarUrl.StartsWith("http")
                ? ImageSource.FromUri(new Uri(avatarUrl))
                : ImageSource.FromUri(new Uri(baseUrl + "/" + avatarUrl.TrimStart('/')));
        }

        Preferences.Set("auth_plan_type", profile.PlanType);
    }

    private async Task LoadHistoryCountAsync()
    {
        var history = await _profileApi.GetHistoryAsync();
        LblHistoryCount.Text = history.Count > 0 ? $"{history.Count} địa điểm đã ghé thăm" : "Xem các địa điểm đã ghé thăm";
    }

    private void UpdatePlanBadge(string planType)
    {
        if (planType == "PRO")
        {
            LblPlanBadge.Text = "🌟 Tài khoản PRO";
            BadgeBorder.BackgroundColor = Color.FromArgb("#FFF8E1");
            UpgradeBanner.IsVisible = false;
        }
        else
        {
            LblPlanBadge.Text = "Tài khoản Miễn phí";
            BadgeBorder.BackgroundColor = Colors.White;
            UpgradeBanner.IsVisible = true;
        }
    }

    private async void OnUpgradeClicked(object sender, EventArgs e) => await Shell.Current.GoToAsync("proupgrade");

    private async void OnHistoryTapped(object sender, EventArgs e) => await Shell.Current.GoToAsync("visitedhistory");

    private async void OnTravelHistoryTapped(object sender, EventArgs e)
    {
        if (!AuthApiClient.IsPro())
        {
            await Shell.Current.GoToAsync("proupgrade");
            return;
        }
        await Shell.Current.GoToAsync("travelhistory");
    }

    private void OnEditProfileTapped(object sender, EventArgs e)
    {
        EditFormPanel.IsVisible = !EditFormPanel.IsVisible;
        if (EditFormPanel.IsVisible) EntryUsername.Text = AuthApiClient.GetCurrentUsername();
    }

    private async void OnPickAvatarTapped(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync();
            if (result is null) return;
            _avatarStream = await result.OpenReadAsync();
            _avatarFileName = result.FileName;
            LblAvatarPicked.Text = $"Đã chọn ảnh: {result.FileName}";
            AvatarImage.Source = ImageSource.FromStream(() => { _avatarStream.Position = 0; return _avatarStream; });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", ex.Message, "OK");
        }
    }

    private void OnCancelEditClicked(object sender, EventArgs e)
    {
        EditFormPanel.IsVisible = false;
        _avatarStream = null;
        _avatarFileName = null;
        LblAvatarPicked.Text = "";
    }

    private async void OnSaveProfileClicked(object sender, EventArgs e)
    {
        var updated = await _profileApi.UpdateProfileAsync(EntryUsername.Text?.Trim(), EntryPhone.Text?.Trim(), _avatarStream, _avatarFileName);
        if (updated is null)
        {
            await DisplayAlertAsync("Lỗi", "Không thể cập nhật. Vui lòng thử lại.", "OK");
            return;
        }
        Preferences.Set("auth_username", updated.Username);
        if (!string.IsNullOrEmpty(updated.AvatarUrl)) Preferences.Set("auth_avatar_url", updated.AvatarUrl);

        OnCancelEditClicked(sender, e);
        await LoadProfileAsync();
        await DisplayAlertAsync("Thành công", "Thông tin đã được cập nhật.", "OK");
    }

    private async void OnLogoutTapped(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync("Đăng xuất", "Bạn có chắc muốn đăng xuất?", "Có", "Không");
        if (!confirm) return;
        AuthApiClient.ClearSession();
        Preferences.Remove("Username");
        Preferences.Remove("Email");
        await Shell.Current.GoToAsync("//map");
    }

    private async void OnChangePasswordClicked(object sender, EventArgs e)
    {
        var current = EntCurrentPassword?.Text?.Trim() ?? "";
        var newPwd  = EntNewPassword?.Text?.Trim() ?? "";
        var confirm = EntConfirmPassword?.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(newPwd))
        {
            await DisplayAlertAsync("Lỗi", "Vui lòng nhập đầy đủ thông tin.", "OK");
            return;
        }
        if (newPwd != confirm)
        {
            await DisplayAlertAsync("Lỗi", "Mật khẩu mới không khớp.", "OK");
            return;
        }
        if (newPwd.Length < 6)
        {
            await DisplayAlertAsync("Lỗi", "Mật khẩu mới phải có ít nhất 6 ký tự.", "OK");
            return;
        }

        try
        {
            var ok = await _profileApi.ChangePasswordAsync(current, newPwd);
            if (ok)
            {
                EntCurrentPassword.Text = "";
                EntNewPassword.Text = "";
                EntConfirmPassword.Text = "";
                await DisplayAlertAsync("Thành công", "Đổi mật khẩu thành công.", "OK");
            }
            else
            {
                await DisplayAlertAsync("Lỗi", "Mật khẩu hiện tại không đúng hoặc có lỗi xảy ra.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Lỗi", ex.Message, "OK");
        }
    }
}