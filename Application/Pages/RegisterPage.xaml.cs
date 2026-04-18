using MauiApp1.Services.Api;

namespace MauiApp1.Pages;

public partial class RegisterPage : ContentPage
{
    private readonly AuthApiClient _auth;
    private FileResult? _avatarFile;

    public RegisterPage(AuthApiClient auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private async void OnPickAvatarClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Chọn ảnh đại diện"
            });
            if (result is null) return;

            _avatarFile = result;
            AvatarPreview.Source = ImageSource.FromFile(result.FullPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Register] PickAvatar: {ex.Message}");
        }
    }

    private void OnTogglePasswordClicked(object sender, EventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        BtnShowPass.Text = PasswordEntry.IsPassword ? "👁" : "🙈";
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        try
        {
            ErrorLabel.IsVisible = false;
            var username = UsernameEntry.Text?.Trim() ?? "";
            var mail     = MailEntry.Text?.Trim() ?? "";
            var phone    = PhoneEntry.Text?.Trim();
            var password = PasswordEntry.Text ?? "";
            var confirm  = ConfirmEntry.Text ?? "";

            if (username.Length < 3)
            {
                ShowError("Tên đăng nhập phải có ít nhất 3 ký tự"); return;
            }
            if (!mail.Contains('@'))
            {
                ShowError("Email không hợp lệ"); return;
            }
            if (password.Length < 6)
            {
                ShowError("Mật khẩu phải có ít nhất 6 ký tự"); return;
            }
            if (password != confirm)
            {
                ShowError("Mật khẩu xác nhận không khớp"); return;
            }

            RegisterBtn.IsEnabled = false;
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            Stream? avatarStream = null;
            string? avatarFileName = null;
            if (_avatarFile is not null)
            {
                avatarStream   = await _avatarFile.OpenReadAsync();
                avatarFileName = _avatarFile.FileName;
            }

            bool ok;
            try
            {
                ok = await _auth.RegisterAsync(username, mail, password, phone, avatarStream, avatarFileName);
            }
            finally
            {
                avatarStream?.Dispose();
            }

            if (!ok)
            {
                ShowError("Đăng ký thất bại. Tên đăng nhập hoặc email đã tồn tại."); return;
            }

            await DisplayAlert("Thành công", "Tài khoản đã được tạo. Vui lòng đăng nhập.", "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ShowError("Lỗi kết nối. Vui lòng thử lại.");
            System.Diagnostics.Debug.WriteLine($"[Register] {ex.Message}");
        }
        finally
        {
            RegisterBtn.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.IsVisible = true;
    }

    private async void OnBackToLoginClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync(".."); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Register] Back: {ex.Message}");
        }
    }

    private async void OnBackToMapClicked(object sender, EventArgs e)
    {
        try { await Shell.Current.GoToAsync("//map"); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Register] Back to map: {ex.Message}");
        }
    }
}
