namespace MauiApp1;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("qrscan", typeof(Pages.QrScanPage));
    }

    // Hàm này tự chạy mỗi khi chuyển màn hình để cập nhật lại tên trên Menu
    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        // Lấy tên tài khoản đã lưu
        var name = Preferences.Get("Username", "");
        var email = Preferences.Get("Email", ""); // Tùy chọn nếu API của bạn trả về Email

        if (!string.IsNullOrEmpty(name))
        {
            MenuUserName.Text = name;
            MenuUserEmail.Text = string.IsNullOrEmpty(email) ? "Thành viên Smart Tourism" : email;
        }
        else
        {
            MenuUserName.Text = "Khách vãng lai";
            MenuUserEmail.Text = "Bạn chưa đăng nhập";
        }
    }

    private void OnHistoryClicked(object sender, EventArgs e)
    {
        // await Shell.Current.GoToAsync("history");
    }

    private void OnChangePasswordClicked(object sender, EventArgs e)
    {
        // await Shell.Current.GoToAsync("changepassword");
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        // Xóa thông tin đăng nhập trong máy
        Preferences.Remove("Username");
        Preferences.Remove("Email");

        // Đóng Menu lại
        Shell.Current.FlyoutIsPresented = false;

        // Cập nhật lại thanh Menu ngay lập tức
        MenuUserName.Text = "Khách du lịch";
        MenuUserEmail.Text = "Bạn chưa đăng nhập";

        // Mở lại trang bản đồ để Top Bar tự làm mới (hiện lại nút Đăng Nhập)
        await Shell.Current.GoToAsync("//map");
    }
}