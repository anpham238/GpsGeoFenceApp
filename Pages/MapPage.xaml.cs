using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Data;                     // 🔧 dùng PoiDbService
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace MauiApp1.Pages;

public partial class MapPage : ContentPage
{
    private readonly IGeofenceService _geofence;
    private readonly ILocationService _location;
    private readonly PoiDbService _poiDbService;   // 🔧 DB service để test kết nối (hoặc load POI)

    private readonly List<Poi> _pois = new();
    private CancellationTokenSource? _cts;
    private Location? _lastLocation;

    // Tọa độ TPHCM - hiển thị ngay lập tức khi mở app
    private static readonly Location _hcmCenter = new(10.776889, 106.700806);

    // 🔧 Thêm PoiDbService vào constructor (DI)
    public MapPage(IGeofenceService geofence, ILocationService location, PoiDbService poiDbService)
    {
        InitializeComponent();

        _geofence = geofence ?? throw new ArgumentNullException(nameof(geofence));
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _poiDbService = poiDbService ?? throw new ArgumentNullException(nameof(poiDbService)); // 🔧

        BtnStart.Clicked += (_, _) => StartUiLoop();
        BtnStop.Clicked += (_, _) => StopUiLoop();

        SeedPoisAndPins();

        _geofence.OnPoiEvent += async (poi, type) =>
            await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlertAsync("Geofence", $"{type}: {poi.Name}", "OK"));
    }

    // ── Seed POI ─────────────────────────────────────────────────────────
    void SeedPoisAndPins()
    {
        _pois.Add(new Poi
        {
            Id = "poi_hcm",
            Name = "TP.HCM",
            Description = "Trung tam",
            Latitude = 10.776889,
            Longitude = 106.700806,
            RadiusMeters = 150,
            NearRadiusMeters = 300,
            DebounceSeconds = 3,
            CooldownSeconds = 30
        });

        _pois.Add(new Poi
        {
            Id = "poi_ntmk",
            Name = "NTMK Park",
            Description = "Cong vien",
            Latitude = 10.787,
            Longitude = 106.700,
            RadiusMeters = 120,
            NearRadiusMeters = 240,
            DebounceSeconds = 3,
            CooldownSeconds = 30
        });

        foreach (var p in _pois)
        {
            MyMap.Pins.Add(new Pin
            {
                Label = $"{p.Name} ({p.RadiusMeters}m)",
                Address = p.Description,
                Location = new Location(p.Latitude, p.Longitude)
            });
        }
    }

    // ── Lifecycle ────────────────────────────────────────────────────────
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // B1: Hiện bản đồ TPHCM NGAY LẬP TỨC - không cần chờ GPS
        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3)));

        // 🔧 B2: TEST kết nối Azure SQL (đoạn bạn hỏi “đặt ở đâu”)
        var ok = await _poiDbService.TestConnectionAsync();
        await DisplayAlertAsync("Azure SQL", ok ? "Kết nối OK" : "Kết nối lỗi", "Đóng");

        // 🔧 B3: Xin quyền Location đúng chuẩn Android 11+
        var hasPerm = await EnsureLocationPermissionsAsync();
        if (!hasPerm) return;

        // B4: Cập nhật về vị trí thực (async, không block UI)
        _ = Task.Run(UpdateToRealLocationAsync);

        // B5: Đăng ký Geofence và bật GPS loop
        await _geofence.RegisterAsync(_pois);
        StartUiLoop();
    }

    protected override void OnDisappearing()
    {
        StopUiLoop();
        base.OnDisappearing();
    }

    // ── Quy trình xin quyền Location đúng Android 11+ ────────────────────
    // Tránh xin 2 quyền cùng lúc (sẽ bị hệ thống bỏ qua); trên Android 11+,
    // background phải xin RIÊNG hoặc hướng người dùng mở Settings.
    private async Task<bool> EnsureLocationPermissionsAsync()
    {
        // Foreground trước
        var when = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (when != PermissionStatus.Granted) return false;

        if (DeviceInfo.Platform == DevicePlatform.Android && OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var always = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (always != PermissionStatus.Granted)
            {
                var open = await DisplayAlertAsync(
                    "Quyền nền",
                    "Để nhận geofence khi app ở nền, cần bật 'Allow all the time' trong Cài đặt.",
                    "Mở cài đặt", "Để sau");

                if (open) AppInfo.ShowSettingsUI();
                return false;
            }
        }
        else
        {
            // Android 10 hoặc iOS: có thể xin trực tiếp
            var always = await Permissions.RequestAsync<Permissions.LocationAlways>();
            if (always != PermissionStatus.Granted) return false;
        }

        return true;
    }

    // ── Cập nhật bản đồ về vị trí GPS thực ───────────────────────────────
    private async Task UpdateToRealLocationAsync()
    {
        try
        {
            var req = new GeolocationRequest(
                GeolocationAccuracy.Best,
                TimeSpan.FromSeconds(10));

            var loc = await Geolocation.GetLocationAsync(req);

            if (loc != null)
            {
                _lastLocation = loc;

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    MyMap.MoveToRegion(
                        MapSpan.FromCenterAndRadius(loc, Distance.FromKilometers(1)));
                });

                System.Diagnostics.Debug.WriteLine(
                    $"[GPS] Vi tri thuc: {loc.Latitude:F6}, {loc.Longitude:F6} " +
                    $"(acc: {loc.Accuracy:F0}m)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GPS] Khong lay duoc vi tri: {ex.Message}");
        }
    }

    // ── GPS Loop chính ───────────────────────────────────────────────────
    void StartUiLoop()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = TrackLoopAsync(_cts.Token);

        _location.StartTracking((lat, lng) =>
        {
            _lastLocation = new Location(lat, lng);
        });
    }

    void StopUiLoop()
    {
        _cts?.Cancel();
        _cts = null;
        _location.StopTracking();
    }

    async Task TrackLoopAsync(CancellationToken token)
    {
        var req = new GeolocationRequest(
            GeolocationAccuracy.Medium,
            TimeSpan.FromSeconds(10));

        while (!token.IsCancellationRequested)
        {
            try
            {
                var loc = await Geolocation.GetLocationAsync(req, token);
                if (loc == null) { await Task.Delay(5000, token); continue; }

                _lastLocation = loc;

                // Camera follow theo user
                await MainThread.InvokeOnMainThreadAsync(() =>
                    MyMap.MoveToRegion(
                        MapSpan.FromCenterAndRadius(loc, Distance.FromMeters(300))));

                // Kiểm tra NEAR
                foreach (var poi in _pois)
                {
                    var dist = Location.CalculateDistance(
                        new Location(poi.Latitude, poi.Longitude),
                        loc,
                        DistanceUnits.Kilometers) * 1000.0;

                    if (dist <= poi.NearRadiusMeters && dist > poi.RadiusMeters)
                    {
                        if (GeofenceEventGate.ShouldAccept(poi.Id, "NEAR",
                                poi.DebounceSeconds, poi.CooldownSeconds))
                        {
                            await MainThread.InvokeOnMainThreadAsync(() =>
                                DisplayAlertAsync("Near POI",
                                    $"Den gan: {poi.Name} (~{dist:F0} m)", "OK"));
                        }
                    }
                }
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"[Loop] {ex.Message}");
            }

            try { await Task.Delay(5000, token); } catch { }
        }
    }
}