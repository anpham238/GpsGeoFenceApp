using MauiApp1.Services.Api;

namespace MauiApp1.Pages;

public partial class AreaPackSelectPage : ContentPage
{
    private readonly ProfileApiClient _profileApi;
    private List<AreaProductDto> _allAreas = [];

    public AreaPackSelectPage(ProfileApiClient profileApi)
    {
        InitializeComponent();
        _profileApi = profileApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAreasAsync();
    }

    private async Task LoadAreasAsync()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        AreaList.IsVisible         = false;
        EmptyState.IsVisible       = false;

        _allAreas = await _profileApi.GetAreasWithProductsAsync();

        // fallback khi API chưa sẵn sàng
        if (_allAreas.Count == 0)
        {
            _allAreas =
            [
                new() { AreaCode="HCMC",    AreaName="TP. Hồ Chí Minh", City="TP. Hồ Chí Minh", ProductCode="AREA_HCMC_24H",    Price=49000, DurationHours=24 },
                new() { AreaCode="HANOI",   AreaName="Hà Nội",           City="Hà Nội",           ProductCode="AREA_HANOI_24H",   Price=59000, DurationHours=24 },
                new() { AreaCode="VUNGTAU", AreaName="Vũng Tàu",         City="Vũng Tàu",         ProductCode="AREA_VUNGTAU_24H", Price=39000, DurationHours=24 },
                new() { AreaCode="DALAT",   AreaName="Đà Lạt",           City="Đà Lạt",           ProductCode="AREA_DALAT_24H",   Price=45000, DurationHours=24 },
            ];
        }

        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ApplyFilter(SearchBox.Text);
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) =>
        ApplyFilter(e.NewTextValue);

    private void ApplyFilter(string? query)
    {
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allAreas
            : _allAreas.Where(a =>
                a.AreaName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                a.City.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        if (filtered.Count == 0)
        {
            AreaList.IsVisible   = false;
            EmptyState.IsVisible = true;
        }
        else
        {
            AreaList.ItemsSource = filtered;
            AreaList.IsVisible   = true;
            EmptyState.IsVisible = false;
        }
    }

    private async void OnBuyAreaClicked(object sender, EventArgs e)
    {
        if (sender is not Button btn || btn.CommandParameter is not AreaProductDto area)
            return;

        await Shell.Current.GoToAsync("payment", new Dictionary<string, object>
        {
            [nameof(PaymentPage.ProductCode)]   = area.ProductCode,
            [nameof(PaymentPage.ProductName)]   = "Area Pack",
            [nameof(PaymentPage.AreaName)]      = area.AreaName,
            [nameof(PaymentPage.Price)]         = $"{area.Price:N0}đ",
            [nameof(PaymentPage.DurationLabel)] = $"{area.DurationHours} giờ"
        });
    }
}
