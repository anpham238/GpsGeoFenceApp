using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Services.Narration;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;
namespace MauiApp1.Pages
{
    public partial class MapPage : ContentPage
    {
        private readonly IGeofenceService _geofence;
        private readonly ILocationService _location;
        private readonly PoiDatabase _db;
        private readonly NarrationManager _narration;
        private readonly List<Poi> _pois = new();
        private readonly Dictionary<string, Pin> _pinMap = new();
        private CancellationTokenSource? _cts;
        private Poi? _nearestPoi;
        private Location? _userLocation;

        private static readonly Location _hcmCenter = new(10.776889, 106.700806);

        // ════════════════════════════════════════════════════════
        // KHOI TAO
        // ════════════════════════════════════════════════════════
        public MapPage(IGeofenceService geofence,
                       ILocationService location,
                       PoiDatabase db,
                       NarrationManager narration)
        {
            InitializeComponent();

            _geofence = geofence ?? throw new ArgumentNullException(nameof(geofence));
            _location = location ?? throw new ArgumentNullException(nameof(location));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _narration = narration ?? throw new ArgumentNullException(nameof(narration));

            BtnStart.Clicked += (_, _) => StartTracking();
            BtnStop.Clicked += (_, _) => StopTracking();
            BtnRefresh.Clicked += async (_, _) => await ReloadPoisAsync();
            BtnDetail.Clicked += (_, _) => ShowDetail(_nearestPoi);
            BtnOpenMap.Clicked += async (_, _) => await OpenMapsAsync(_nearestPoi);
            BtnDetailOpenMap.Clicked += async (_, _) => await OpenMapsAsync(_nearestPoi);
            BtnCloseDetail.Clicked += (_, _) => HideDetail();

            // Toolbar Reset về mốc HCM (hoặc tọa độ nhà bạn nếu thay _hcmCenter)
            ToolbarItems.Add(new ToolbarItem
            {
                Text = "Reset",
                Order = ToolbarItemOrder.Primary,
                Command = new Command(() =>
                    MyMap.MoveToRegion(
                        MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3))))
            });

            // ── Geofence ENTER / NEAR / EXIT ────────────────────────────────
            _geofence.OnPoiEvent += async (poi, type) =>
            {
                if (type == "EXIT")
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ClearHighlight();
                        HideBanner();
                    });
                    return;
                }

                await MainThread.InvokeOnMainThreadAsync(() =>
                    HighlightPoi(poi, $"Vào vùng {type}"));

                // Dùng NarrationManager: ưu tiên AudioUrl (nếu có) → TTS fallback
                await _narration.HandleAsync(new Announcement(
                    poi,
                    type == "ENTER" ? PoiEventType.Enter : PoiEventType.Near,
                    DateTime.UtcNow));
            };
        }

        // ════════════════════════════════════════════════════════
        // LIFECYCLE
        // ════════════════════════════════════════════════════════
        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // 1) Hiển thị HCM ngay
            MyMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3)));

            // 2) Xin quyền (Android 11+: tách bước, mở Settings nếu cần nền)
            if (!await EnsureLocationPermissionsAsync()) return;

            // 2.1) Bật dot sau khi Granted (tránh xin trùng)
            await MainThread.InvokeOnMainThreadAsync(() => MyMap.IsShowingUser = true);

            // 3) Load POI từ SQLite
            await ReloadPoisAsync();

            // 4) Lấy vị trí thực (nền) – KHÔNG kéo camera để lướt map tự do
            _ = Task.Run(MoveToRealLocationAsync);

            // 5) Đăng ký geofence + loop
            await _geofence.RegisterAsync(_pois);
            StartTracking();
        }

        protected override void OnDisappearing()
        {
            StopTracking();
            base.OnDisappearing();
        }

        // ════════════════════════════════════════════════════════
        // QUYỀN LOCATION (Android 11+ tách bước)
        // ════════════════════════════════════════════════════════
        private async Task<bool> EnsureLocationPermissionsAsync()
        {
            // Foreground trước
            var when = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (when != PermissionStatus.Granted) return false;

            if (DeviceInfo.Platform == DevicePlatform.Android && OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                // Android 11+: không xin kèm background; hướng người dùng bật trong Settings
                var always = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            }
            else
            {
                // Android 10 hoặc iOS: có thể xin trực tiếp
                var always = await Permissions.RequestAsync<Permissions.LocationAlways>();
                if (always != PermissionStatus.Granted) return false;
            }
            return true;
        }

        // Helper để dọn cảnh báo obsolete (MapPage.DisplayAlertAsync mới)
        private Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel = "Đóng")
            => DisplayAlertAsync(title, message, accept, cancel);

        // ════════════════════════════════════════════════════════
        // LOAD POI TỪ SQLITE
        // ════════════════════════════════════════════════════════
        private async Task ReloadPoisAsync()
        {
            try
            {
                // DB trả ORDER BY Priority ASC
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

                        // TAP pin: highlight + chi tiết + phát thuyết minh
                        pin.MarkerClicked += async (_, e) =>
                        {
                            e.HideInfoWindow = false;
                            HighlightPoi(p, "Đã chọn");
                            ShowDetail(p);

                            await _narration.HandleAsync(new Announcement(
                                p, PoiEventType.Tap, DateTime.UtcNow));
                        };

                        _pinMap[p.Id] = pin;
                        MyMap.Pins.Add(pin);
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[Map] {_pois.Count} POI (pri {_pois.FirstOrDefault()?.Priority}→{_pois.LastOrDefault()?.Priority})");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Map] Reload lỗi: {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════
        // VỊ TRÍ NGƯỜI DÙNG (lần định vị đầu – không kéo camera)
        // ════════════════════════════════════════════════════════
        private async Task MoveToRealLocationAsync()
        {
            try
            {
                var req = new GeolocationRequest(
                    GeolocationAccuracy.Best, TimeSpan.FromSeconds(10));
                var loc = await Geolocation.GetLocationAsync(req);
                if (loc == null) return;

                _userLocation = loc;

                // KHÔNG MoveToRegion ở đây để tránh auto-follow
                System.Diagnostics.Debug.WriteLine(
                    $"[GPS] {loc.Latitude:F6}, {loc.Longitude:F6} acc={loc.Accuracy:F0}m");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GPS] {ex.Message}");
            }
        }

        // ════════════════════════════════════════════════════════
        // GPS LOOP – NEAR + ƯU TIÊN (KHÔNG kéo camera)
        // ════════════════════════════════════════════════════════
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
            var req = new GeolocationRequest(
                GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var loc = await Geolocation.GetLocationAsync(req, token);
                    if (loc == null) { await Task.Delay(5000, token); continue; }

                    _userLocation = loc;

                    // Tìm POI ưu tiên cao nhất trong vùng NEAR
                    // _pois đã sort theo Priority ASC từ DB
                    Poi? nearest = null;
                    double nearestDist = double.MaxValue;

                    foreach (var poi in _pois.ToList())
                    {
                        var dist = Location.CalculateDistance(
                            new Location(poi.Latitude, poi.Longitude),
                            loc, DistanceUnits.Kilometers) * 1000.0; // km -> m

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
                            HighlightPoi(nearest, $"Đến gần (~{nearestDist:F0}m) • Ưu tiên #{nearest.Priority}"));

                        if (GeofenceEventGate.ShouldAccept(nearest.Id, "NEAR",
                                nearest.DebounceSeconds, nearest.CooldownSeconds))
                        {
                            await _narration.HandleAsync(new Announcement(
                                nearest, PoiEventType.Near, DateTime.UtcNow));
                        }
                    }
                    else
                    {
                        await MainThread.InvokeOnMainThreadAsync(() =>
                        {
                            ClearHighlight();
                            HideBanner();
                        });
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
        // HIGHLIGHT
        // ════════════════════════════════════════════════════════
        private void HighlightPoi(Poi poi, string status)
        {
            ClearHighlight();
            _nearestPoi = poi;

            if (_pinMap.TryGetValue(poi.Id, out var pin))
                pin.Label = $"★ {poi.Name}";

            ShowBanner(poi, status);
        }

        private void ClearHighlight()
        {
            if (_nearestPoi == null) return;

            if (_pinMap.TryGetValue(_nearestPoi.Id, out var pin))
                pin.Label = _nearestPoi.Name;

            _nearestPoi = null;
        }

        // ════════════════════════════════════════════════════════
        // BANNER & DETAIL
        // ════════════════════════════════════════════════════════
        private void ShowBanner(Poi poi, string status)
        {
            LblPoiName.Text = poi.Name;
            LblPoiDist.Text = status;
            BtnOpenMap.IsVisible = !string.IsNullOrWhiteSpace(poi.MapLink);
            PoiBanner.IsVisible = true;
        }

        private void HideBanner() => PoiBanner.IsVisible = false;

        private void ShowDetail(Poi? poi)
        {
            if (poi == null) return;

            DetailName.Text = poi.Name;
            DetailDesc.Text = string.IsNullOrWhiteSpace(poi.Description)
                                ? "(Không có mô tả)"
                                : poi.Description;
            DetailCoord.Text = $"📍 {poi.Latitude:F6}, {poi.Longitude:F6}";

            var audioMode = !string.IsNullOrWhiteSpace(poi.AudioUrl)
                ? "🎵 File audio"
                : "🗣 TTS";
            DetailRadius.Text =
                $"🔵 Bán kính: {poi.RadiusMeters}m  |  Gần: {poi.NearRadiusMeters}m  |  Ưu tiên: #{poi.Priority}\n{audioMode}";

            BtnDetailOpenMap.IsVisible = !string.IsNullOrWhiteSpace(poi.MapLink);
            DetailPanel.IsVisible = true;

            // Cho phép zoom đến khi người dùng xem chi tiết (tác vụ chủ động)
            MyMap.MoveToRegion(
                MapSpan.FromCenterAndRadius(
                    new Location(poi.Latitude, poi.Longitude),
                    Distance.FromMeters(400)));
        }

        private void HideDetail() => DetailPanel.IsVisible = false;

        private async Task OpenMapsAsync(Poi? poi)
        {
            if (poi == null) return;
            var url = !string.IsNullOrWhiteSpace(poi.MapLink)
                ? poi.MapLink
                : $"https://maps.google.com/?q={poi.Latitude},{poi.Longitude}";
            try { await Launcher.OpenAsync(new Uri(url)); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Maps] {ex.Message}");
            }
        }
    }
}