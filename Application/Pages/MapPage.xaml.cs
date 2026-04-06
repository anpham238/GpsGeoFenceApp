using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Services.Api;
using MauiApp1.Services.Narration;
using MauiApp1.Services.Sync;

using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MauiApp1.Pages;

public partial class MapPage : ContentPage
{
    private readonly IGeofenceService _geofence;
    private readonly ILocationService _location;
    private readonly PoiDatabase _db;
    private readonly NarrationManager _narration;
    private readonly PoiSyncService _poiSync;            // offline-first sync
    private readonly PlaybackApiClient _playback;       

    private readonly List<Poi> _pois = new();
    private readonly Dictionary<string, Pin> _pinMap = new();

    private CancellationTokenSource? _cts;
    private Poi? _nearestPoi;
    private Location? _userLocation;

    private static readonly Location _hcmCenter = new(10.776889, 106.700806);

    // ── Bottom sheet state ───────────────────────────────────────────
    double _sheetCollapsedOffset = -1;
    readonly double _sheetExpandedOffset = 0;
    double _sheetStartPanY = 0;
    bool _sheetReady = false;
    const double SheetPeekHeight = 140;

    public MapPage(
        IGeofenceService geofence,
        ILocationService location,
        PoiDatabase db,
        NarrationManager narration,
        PoiSyncService poiSync,
        PlaybackApiClient playback)   // ✅ thêm param inject
    {
        InitializeComponent();

        _geofence = geofence ?? throw new ArgumentNullException(nameof(geofence));
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _narration = narration ?? throw new ArgumentNullException(nameof(narration));
        _poiSync = poiSync ?? throw new ArgumentNullException(nameof(poiSync));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback)); // ✅ gán field

        // Toolbar: Reset camera + Sync POI
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Reset",
            Order = ToolbarItemOrder.Primary,
            Command = new Command(() => MyMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3))))
        });
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "QR",
            Order = ToolbarItemOrder.Primary,
            Command = new Command(async () =>
                await Shell.Current.GoToAsync("qrscan"))
        });
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Sync",
            Order = ToolbarItemOrder.Secondary,
            Command = new Command(async () =>
            {
                await _poiSync.SyncOnceAsync();
                await ReloadPoisAsync();
                await _geofence.RegisterAsync(_pois); // cập nhật geofence theo POI mới
            })
        });

        // Bottom sheet
        BtnOpenInMaps.Clicked += async (_, _) => await OpenMapsAsync(_nearestPoi);
        BottomSheet.SizeChanged += (_, _) => SetupBottomSheetOffsets();
        this.SizeChanged += (_, _) => SetupBottomSheetOffsets();

        // Geofence ENTER / NEAR / EXIT
        _geofence.OnPoiEvent += async (poi, type) =>
        {
            if (type == "EXIT")
            {
                await MainThread.InvokeOnMainThreadAsync(ClearHighlight);
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                HighlightPoi(poi, $"Vào vùng {type}");
                ShowDetail(poi);
            });

            var evType = type == "ENTER" ? PoiEventType.Enter : PoiEventType.Near;
            var started = DateTime.UtcNow;

            await _narration.HandleAsync(new Announcement(poi, evType, started));

            // ✅ Log playback (fire-and-forget, không crash nếu offline vì client tự try/catch)
            var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
            _ = _playback.LogAsync(poi.Id, type, dur > 0 ? dur : null);
        };
    }

    // ════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════════
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3)));

        if (!await EnsureLocationPermissionsAsync()) return;
        await MainThread.InvokeOnMainThreadAsync(() => MyMap.IsShowingUser = true);

        // Load POI (từ SQLite - offline-first)
        await ReloadPoisAsync();

        // Di chuyển về vị trí GPS thực
        _ = Task.Run(MoveToRealLocationAsync);
        if (_pois.Count > 0)
        {
            await _geofence.RegisterAsync(_pois);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[Geofence] Skip register: no POIs.");
        }
        StartTracking();
    }
    protected override void OnDisappearing()
    {
        StopTracking();
        base.OnDisappearing();
    }

    private async Task<bool> EnsureLocationPermissionsAsync()
    {
        var when = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (when != PermissionStatus.Granted) return false;

        if (DeviceInfo.Platform == DevicePlatform.Android
            && OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var always = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        }
        else
        {
            await Permissions.RequestAsync<Permissions.LocationAlways>();
        }

        return true;
    }

    // ════════════════════════════════════════════════════════════════
    // LOAD POI TỪ SQLITE
    // ════════════════════════════════════════════════════════════════
    private async Task ReloadPoisAsync()
    {
        try
        {
            var pois = await _db.GetActivePoisAsync();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _pois.Clear();
                _pinMap.Clear();
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

                    pin.MarkerClicked += async (_, e) =>
                    {
                        e.HideInfoWindow = false;

                        HighlightPoi(p, "Đã chọn");
                        ShowDetail(p);

                        var started = DateTime.UtcNow;
                        await _narration.HandleAsync(new Announcement(p, PoiEventType.Tap, started));

                        // ✅ Log TAP
                        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
                        _ = _playback.LogAsync(p.Id, "TAP", dur > 0 ? dur : null);
                    };

                    _pinMap[p.Id] = pin;
                    MyMap.Pins.Add(pin);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] Reload lỗi: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════
    // GPS – VỊ TRÍ NGƯỜI DÙNG
    // ════════════════════════════════════════════════════════════════
    private async Task MoveToRealLocationAsync()
    {
        try
        {
            var req = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
            var loc = await Geolocation.GetLocationAsync(req);
            if (loc == null) return;

            _userLocation = loc;

            await MainThread.InvokeOnMainThreadAsync(() =>
                MyMap.MoveToRegion(
                    MapSpan.FromCenterAndRadius(loc, Distance.FromKilometers(1))));

            System.Diagnostics.Debug.WriteLine(
                $"[GPS] {loc.Latitude:F6}, {loc.Longitude:F6} acc={loc.Accuracy:F0}m");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GPS] {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════════
    // GPS LOOP – CAMERA FOLLOW + NEAR + PRIORITY
    // ════════════════════════════════════════════════════════════════
    private void StartTracking()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _ = TrackLoopAsync(_cts.Token);

        _location.StartTracking((lat, lng) =>
            _userLocation = new Location(lat, lng));
    }

    private void StopTracking()
    {
        _cts?.Cancel();
        _cts = null;
        _location.StopTracking();
    }

    private async Task TrackLoopAsync(CancellationToken token)
    {
        var req = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));

        while (!token.IsCancellationRequested)
        {
            try
            {
                var loc = await Geolocation.GetLocationAsync(req, token);
                if (loc == null) { await Task.Delay(5000, token); continue; }

                _userLocation = loc;

                //await MainThread.InvokeOnMainThreadAsync(() =>
                //    MyMap.MoveToRegion(
                //        MapSpan.FromCenterAndRadius(loc, Distance.FromMeters(350))));

                // ── Tìm POI ưu tiên cao nhất trong vùng NEAR ────────────
                Poi? nearest = null;
                double nearestDist = double.MaxValue;

                foreach (var poi in _pois.ToList())
                {
                    var dist = Location.CalculateDistance(
                        new Location(poi.Latitude, poi.Longitude),
                        loc, DistanceUnits.Kilometers) * 1000.0;

                    if (dist > poi.NearRadiusMeters) continue;

                    var thisPri = poi.Priority ?? 999;
                    var bestPri = nearest?.Priority ?? 999;

                    if (nearest == null
                        || thisPri < bestPri
                        || (thisPri == bestPri && dist < nearestDist))
                    {
                        nearest = poi;
                        nearestDist = dist;
                    }
                }

                if (nearest != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        HighlightPoi(nearest,
                            $"Đến gần (~{nearestDist:F0}m) • Ưu tiên #{nearest.Priority}");
                        ShowDetail(nearest);
                    });

                    if (GeofenceEventGate.ShouldAccept(nearest.Id, "NEAR",
                            nearest.DebounceSeconds, nearest.CooldownSeconds))
                    {
                        var started = DateTime.UtcNow;
                        await _narration.HandleAsync(
                            new Announcement(nearest, PoiEventType.Near, started));

                        // ✅ Log NEAR (gate đã chống spam)
                        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
                        _ = _playback.LogAsync(nearest.Id, "NEAR", dur > 0 ? dur : null);
                    }
                }
                else
                {
                    await MainThread.InvokeOnMainThreadAsync(ClearHighlight);
                }
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine($"[Loop] {ex.Message}");
            }

            try { await Task.Delay(5000, token); } catch { }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // HIGHLIGHT
    // ════════════════════════════════════════════════════════════════
    private void HighlightPoi(Poi poi, string status)
    {
        ClearHighlight();
        _nearestPoi = poi;

        if (_pinMap.TryGetValue(poi.Id, out var pin))
            pin.Label = $"★ {poi.Name}";
    }

    private void ClearHighlight()
    {
        if (_nearestPoi == null) return;

        if (_pinMap.TryGetValue(_nearestPoi.Id, out var pin))
            pin.Label = _nearestPoi.Name;

        _nearestPoi = null;
    }

    // ════════════════════════════════════════════════════════════════
    // BOTTOM SHEET
    // ════════════════════════════════════════════════════════════════
    private void ShowDetail(Poi? poi)
    {
        if (poi == null) return;

        DetailName.Text = poi.Name;
        DetailDesc.Text = string.IsNullOrWhiteSpace(poi.Description)
            ? "(Không có mô tả)"
            : poi.Description;

        DetailCoord.Text = $"📍 {poi.Latitude:F6}, {poi.Longitude:F6}";
        DetailRadius.Text = $"🔵 Bán kính: {poi.RadiusMeters}m | Gần: {poi.NearRadiusMeters}m";

        DetailImage.Source = !string.IsNullOrWhiteSpace(poi.ImageUrl) ? poi.ImageUrl : null;

        var link = !string.IsNullOrWhiteSpace(poi.MapLink)
            ? poi.MapLink
            : $"https://maps.google.com/?q={poi.Latitude},{poi.Longitude}";

        LblPoiLink.Text = link;

        _ = ExpandSheetAsync();
        MyMap.MoveToRegion(
            MapSpan.FromCenterAndRadius(
                new Location(poi.Latitude, poi.Longitude),
                Distance.FromMeters(400)));
    }

    private async Task OpenMapsAsync(Poi? poi)
    {
        if (poi == null) return;

        var url = !string.IsNullOrWhiteSpace(poi.MapLink)
            ? poi.MapLink
            : $"https://maps.google.com/?q={poi.Latitude},{poi.Longitude}";

        try { await Launcher.OpenAsync(new Uri(url)); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Maps] {ex.Message}"); }
    }

    void SetupBottomSheetOffsets()
    {
        if (BottomSheet.Height <= 0 || this.Height <= 0) return;

        _sheetCollapsedOffset = Math.Max(0, BottomSheet.Height - SheetPeekHeight);

        if (!_sheetReady)
        {
            BottomSheet.TranslationY = _sheetCollapsedOffset;
            _sheetReady = true;
        }
    }

    Task ExpandSheetAsync() =>
        BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 180, Easing.CubicOut);

    Task CollapseSheetAsync() =>
        BottomSheet.TranslateToAsync(0, _sheetCollapsedOffset, 180, Easing.CubicOut);

    void BottomSheet_TapHeader(object? sender, EventArgs e)
    {
        if (!_sheetReady) return;

        var isCollapsed = Math.Abs(BottomSheet.TranslationY - _sheetCollapsedOffset) < 1;
        _ = BottomSheet.TranslateToAsync(0,
            isCollapsed ? _sheetExpandedOffset : _sheetCollapsedOffset,
            200, Easing.CubicOut);
    }

    void BottomSheet_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!_sheetReady) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _sheetStartPanY = BottomSheet.TranslationY;
                break;

            case GestureStatus.Running:
                var newY = Math.Clamp(_sheetStartPanY + e.TotalY,
                    _sheetExpandedOffset, _sheetCollapsedOffset);
                BottomSheet.TranslationY = newY;
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var half = (_sheetCollapsedOffset - _sheetExpandedOffset) / 2.0;
                var curr = BottomSheet.TranslationY - _sheetExpandedOffset;
                _ = BottomSheet.TranslateToAsync(0,
                    curr <= half ? _sheetExpandedOffset : _sheetCollapsedOffset,
                    180, Easing.CubicOut);
                break;
        }
    }

}
