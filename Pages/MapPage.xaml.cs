using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace MauiApp1.Pages;

public partial class MapPage : ContentPage
{
    private readonly IGeofenceService _geofence;
    private readonly ILocationService _location;
    private readonly PoiDatabase _db;

    private readonly List<Poi> _pois = new();
    private CancellationTokenSource? _cts;

    // POI hien dang hien thi tren banner
    private Poi? _nearestPoi;

    // Vi tri mac dinh TPHCM hien thi ngay khi mo app
    private static readonly Location _hcmCenter = new(10.776889, 106.700806);

    // ════════════════════════════════════════════════════════
    // KHOI TAO
    // ════════════════════════════════════════════════════════
    public MapPage(IGeofenceService geofence, ILocationService location, PoiDatabase db)
    {
        InitializeComponent();

        _geofence = geofence ?? throw new ArgumentNullException(nameof(geofence));
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _db = db ?? throw new ArgumentNullException(nameof(db));

        // Nut dieu khien
        BtnStart.Clicked += (_, _) => StartUiLoop();
        BtnStop.Clicked += (_, _) => StopUiLoop();
        BtnRefresh.Clicked += async (_, _) => await ReloadPoisAsync();
        BtnOpenMap.Clicked += (_, _) => OpenMapLink(_nearestPoi);

        // Geofence (ENTER / DWELL / EXIT) -> TTS + cap nhat banner
        _geofence.OnPoiEvent += async (poi, type) =>
        {
            if (type == "EXIT")
            {
                await MainThread.InvokeOnMainThreadAsync(() => HideBanner());
                return;
            }
            // Hien banner + doc TTS
            await MainThread.InvokeOnMainThreadAsync(() =>
                ShowBanner(poi, $"Vao vung {type}"));
            await PlayTtsAsync(poi);
        };
    }

    // ════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Hien ban do TPHCM ngay lap tuc (khong can cho GPS)
        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3)));

        // 2. Xin quyen GPS
        var when = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (when != PermissionStatus.Granted) return;
        _ = await Permissions.RequestAsync<Permissions.LocationAlways>();

        // 3. Load POI tu SQLite -> hien len ban do
        await ReloadPoisAsync();

        // 4. Cap nhat ban do ve vi tri GPS thuc (chay nen)
        _ = Task.Run(UpdateToRealLocationAsync);

        // 5. Dang ky Geofence + bat GPS loop
        await _geofence.RegisterAsync(_pois);
        StartUiLoop();
    }

    protected override void OnDisappearing()
    {
        StopUiLoop();
        base.OnDisappearing();
    }

    // ════════════════════════════════════════════════════════
    // DATABASE -> BAN DO
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Doc POI tu SQLite va hien len ban do.
    /// SQLite tu dong seed 7 diem TPHCM lan dau tien.
    /// </summary>
    private async Task ReloadPoisAsync()
    {
        try
        {
            var pois = await _db.GetActivePoisAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _pois.Clear();
                MyMap.Pins.Clear();

                foreach (var p in pois)
                {
                    _pois.Add(p);

                    var pin = new Pin
                    {
                        Label = p.Name,
                        Address = p.Description,
                        Location = new Location(p.Latitude, p.Longitude),
                        Type = PinType.Place
                    };

                    // Nhan vao pin -> hien banner + hoi mo Google Maps
                    pin.MarkerClicked += async (_, e) =>
                    {
                        e.HideInfoWindow = false;
                        ShowBanner(p, "Da chon");

                        if (!string.IsNullOrWhiteSpace(p.MapLink))
                        {
                            bool mo = await DisplayAlertAsync(
                                p.Name,
                                p.Description,
                                "Mo Google Maps", "Dong");
                            if (mo) await Launcher.OpenAsync(new Uri(p.MapLink));
                        }
                    };

                    MyMap.Pins.Add(pin);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[Map] Hien {_pois.Count} POI tren ban do");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] Reload loi: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════
    // GPS - VI TRI THUC
    // ════════════════════════════════════════════════════════

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
                await MainThread.InvokeOnMainThreadAsync(() =>
                    MyMap.MoveToRegion(
                        MapSpan.FromCenterAndRadius(loc, Distance.FromKilometers(1))));

                System.Diagnostics.Debug.WriteLine(
                    $"[GPS] {loc.Latitude:F6}, {loc.Longitude:F6} acc={loc.Accuracy:F0}m");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GPS] {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════
    // GPS LOOP - NEAR DETECTION + CAMERA FOLLOW
    // ════════════════════════════════════════════════════════

    private void StartUiLoop()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = TrackLoopAsync(_cts.Token);
        _location.StartTracking((lat, lng) =>
            System.Diagnostics.Debug.WriteLine($"[Fused] {lat:F5},{lng:F5}"));
    }

    private void StopUiLoop()
    {
        _cts?.Cancel();
        _cts = null;
        _location.StopTracking();
    }

    private async Task TrackLoopAsync(CancellationToken token)
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

                // Camera follow user
                await MainThread.InvokeOnMainThreadAsync(() =>
                    MyMap.MoveToRegion(
                        MapSpan.FromCenterAndRadius(loc, Distance.FromMeters(300))));

                // Kiem tra NEAR cho tung POI
                foreach (var poi in _pois.ToList())
                {
                    var dist = Location.CalculateDistance(
                        new Location(poi.Latitude, poi.Longitude),
                        loc, DistanceUnits.Kilometers) * 1000.0;

                    // Vung NEAR: giua NearRadius va RadiusMeters
                    if (dist <= poi.NearRadiusMeters && dist > poi.RadiusMeters)
                    {
                        if (GeofenceEventGate.ShouldAccept(poi.Id, "NEAR",
                                poi.DebounceSeconds, poi.CooldownSeconds))
                        {
                            // Hien banner "Den gan"
                            await MainThread.InvokeOnMainThreadAsync(() =>
                                ShowBanner(poi, $"Den gan (~{dist:F0}m)"));

                            // Phat TTS
                            await PlayTtsAsync(poi);
                        }
                    }
                    // Ra khoi tat ca vung -> an banner
                    else if (dist > poi.NearRadiusMeters && _nearestPoi?.Id == poi.Id)
                    {
                        await MainThread.InvokeOnMainThreadAsync(() => HideBanner());
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

    // ════════════════════════════════════════════════════════
    // NARRATION - TTS
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Phat thuyet minh POI:
    /// - Co AudioUrl -> phat file audio (mp3/wav)
    /// - Khong co    -> doc TTS bang TextToSpeech built-in
    /// </summary>
    private async Task PlayTtsAsync(Poi poi)
    {
        try
        {
            // Neu co file audio (Azure Blob / URL truc tiep)
            if (!string.IsNullOrWhiteSpace(poi.AudioUrl))
            {
                System.Diagnostics.Debug.WriteLine($"[Audio] URL: {poi.AudioUrl}");
                // TODO: goi AudioPlayerService.PlayAsync(poi.AudioUrl) khi san sang
                return;
            }

            // TTS fallback
            var text = !string.IsNullOrWhiteSpace(poi.NarrationText)
                ? poi.NarrationText
                : $"Ban dang den {poi.Name}. {poi.Description}";

            System.Diagnostics.Debug.WriteLine($"[TTS] Doc: {text[..Math.Min(40, text.Length)]}...");

            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions
            {
                Volume = 1.0f,
                Pitch = 1.0f
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Loi: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════
    // BANNER UI
    // ════════════════════════════════════════════════════════

    private void ShowBanner(Poi poi, string status)
    {
        _nearestPoi = poi;
        LblPoiName.Text = $"{poi.Name}";
        LblPoiDist.Text = status;
        BtnOpenMap.IsVisible = !string.IsNullOrWhiteSpace(poi.MapLink);
        PoiBanner.IsVisible = true;
    }

    private void HideBanner()
    {
        _nearestPoi = null;
        PoiBanner.IsVisible = false;
    }

    // ════════════════════════════════════════════════════════
    // MO GOOGLE MAPS
    // ════════════════════════════════════════════════════════

    private async void OpenMapLink(Poi? poi)
    {
        if (poi == null) return;

        var url = !string.IsNullOrWhiteSpace(poi.MapLink)
            ? poi.MapLink
            : $"https://maps.google.com/?q={poi.Latitude},{poi.Longitude}";

        try { await Launcher.OpenAsync(new Uri(url)); }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] Mo link loi: {ex.Message}");
        }
    }
}