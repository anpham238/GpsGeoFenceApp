using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using MauiApp1.Services.Api;

namespace MauiApp1.Pages;

public partial class TravelHistoryPage : ContentPage
{
    private readonly ProfileApiClient _profileApi;

    public TravelHistoryPage(ProfileApiClient profileApi)
    {
        InitializeComponent();
        _profileApi = profileApi;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTravelHistoryAsync();
    }

    private async Task LoadTravelHistoryAsync()
    {
        LoadingOverlay.IsVisible = true;

        var sessionIdStr = Preferences.Get("analytics_session_id", "");
        if (!Guid.TryParse(sessionIdStr, out var sessionId))
        {
            LoadingOverlay.IsVisible = false;
            LblPointCount.Text = "Không có dữ liệu hành trình";
            return;
        }

        var points = await _profileApi.GetTravelHistoryAsync(sessionId);

        LoadingOverlay.IsVisible = false;

        if (points.Count < 2)
        {
            LblPointCount.Text = "Chưa có đủ dữ liệu hành trình";
            LblDateRange.Text  = "Di chuyển để ghi nhận tuyến đường";
            return;
        }

        LblPointCount.Text = $"{points.Count} điểm GPS";

        var first = points.First().RecordedAt.ToLocalTime();
        var last  = points.Last().RecordedAt.ToLocalTime();
        LblDateRange.Text = $"{first:dd/MM HH:mm} → {last:HH:mm}";

        DrawRedPolyline(points);
        ZoomToRoute(points);
    }

    private void DrawRedPolyline(List<RoutePointDto> points)
    {
        HistoryMap.MapElements.Clear();

        var polyline = new Polyline
        {
            StrokeColor = Colors.Red,
            StrokeWidth = 4
        };

        foreach (var p in points)
            polyline.Geopath.Add(new Location(p.Latitude, p.Longitude));

        HistoryMap.MapElements.Add(polyline);

        // Điểm bắt đầu
        HistoryMap.Pins.Clear();
        var startPin = new Pin
        {
            Label    = "Bắt đầu",
            Location = new Location(points.First().Latitude, points.First().Longitude),
            Type     = PinType.Place
        };
        var endPin = new Pin
        {
            Label    = "Kết thúc",
            Location = new Location(points.Last().Latitude, points.Last().Longitude),
            Type     = PinType.Place
        };
        HistoryMap.Pins.Add(startPin);
        HistoryMap.Pins.Add(endPin);
    }

    private void ZoomToRoute(List<RoutePointDto> points)
    {
        var lats = points.Select(p => p.Latitude).ToList();
        var lngs = points.Select(p => p.Longitude).ToList();

        var centerLat = (lats.Min() + lats.Max()) / 2;
        var centerLng = (lngs.Min() + lngs.Max()) / 2;
        var spanLat   = Math.Max(lats.Max() - lats.Min(), 0.005) * 1.3;
        var spanLng   = Math.Max(lngs.Max() - lngs.Min(), 0.005) * 1.3;

        HistoryMap.MoveToRegion(new MapSpan(
            new Location(centerLat, centerLng), spanLat, spanLng));
    }
}
