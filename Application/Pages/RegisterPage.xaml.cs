using MauiApp1.Services.Api;
namespace MauiApp1.Pages;
public partial class RegisterPage : ContentPage
{
    private readonly AuthApiClient _auth;

    public RegisterPage(AuthApiClient auth)
    {
        InitializeComponent();
        _auth = auth;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        try
        {
            ErrorLabel.IsVisible = false;
            var username = UsernameEntry.Text?.Trim() ?? "";
            var mail = MailEntry.Text?.Trim() ?? "";
            var password = PasswordEntry.Text ?? "";
            var confirm = ConfirmEntry.Text ?? "";

            if (username.Length < 3)
            {
                ErrorLabel.Text = "Tên đăng nhập phải có ít nhất 3 ký tự";
                ErrorLabel.IsVisible = true; return;
            }
            if (!mail.Contains('@'))
            {
                ErrorLabel.Text = "Email không hợp lệ";
                ErrorLabel.IsVisible = true; return;
            }
            if (password.Length < 6)
            {
                ErrorLabel.Text = "Mật khẩu phải có ít nhất 6 ký tự";
                ErrorLabel.IsVisible = true; return;
            }
            if (password != confirm)
            {
                ErrorLabel.Text = "Mật khẩu xác nhận không khớp";
                ErrorLabel.IsVisible = true; return;
            }

            RegisterBtn.IsEnabled = false;
            LoadingIndicator.IsVisible = true;
            LoadingIndicator.IsRunning = true;

            var ok = await _auth.RegisterAsync(username, mail, password);
            if (!ok)
            {
                ErrorLabel.Text = "Đăng ký thất bại. Tên đăng nhập hoặc email đã tồn tại.";
                ErrorLabel.IsVisible = true;
                return;
            }

            await DisplayAlert("Thành công", "Tài khoản đã được tạo. Vui lòng đăng nhập.", "OK");
            await Shell.Current.GoToAsync("..");  // quay về LoginPage+
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = "Lỗi kết nối. Vui lòng thử lại.";
            ErrorLabel.IsVisible = true;
            System.Diagnostics.Debug.WriteLine($"[Register] {ex.Message}");
        }
        finally
        {
            RegisterBtn.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
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
        try
        {
            await Shell.Current.GoToAsync("//map");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Register] Back to map: {ex.Message}");
        }
    }
}
