using MauiApp1.Services.Api;

namespace MauiApp1.Pages;

public partial class VisitedHistoryPage : ContentPage
{
    private readonly ProfileApiClient _profileApi;

    public VisitedHistoryPage(ProfileApiClient profileApi)
    {
        InitializeComponent();
        _profileApi = profileApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        var data = await _profileApi.GetHistoryAsync();
        var viewData = data.Select(x => new HistoryItemVm
        {
            PoiName = x.PoiName,
            LastVisitedAtDisplay = x.LastVisitedAt.ToLocalTime().ToString("HH:mm - dd/MM/yyyy")
        }).ToList();
        HistoryCollection.ItemsSource = viewData;
    }

    private async void OnCloseProfileClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//map");
    }

    private sealed class HistoryItemVm
    {
        public string PoiName { get; set; } = "";
        public string LastVisitedAtDisplay { get; set; } = "";
    }
}
