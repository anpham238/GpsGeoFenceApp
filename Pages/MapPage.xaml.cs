using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Services.Narration;
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
    private readonly Dictionary<string, Pin> _pinMap = new(); // poiId -> Pin
    private CancellationTokenSource? _cts;
    private Poi? _nearestPoi;       // POI dang highlight
    private Location? _userLocation;     // vi tri user hien tai
    private readonly NarrationManager _narration;
    private readonly SemaphoreSlim _ttsGate = new(1, 1);
    // cho phép hủy TTS đang phát (khi người dùng rời vùng/ấn Stop)
    private CancellationTokenSource? _ttsCts;
    private static readonly Location _hcmCenter = new(10.776889, 106.700806);

    // ════════════════════════════════════════════════════════
    // KHOI TAO
    // ════════════════════════════════════════════════════════
    public MapPage(IGeofenceService geofence, ILocationService location, PoiDatabase db, NarrationManager narration)
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
        BtnOpenMap.Clicked += (_, _) => _ = OpenMapsAsync(_nearestPoi);
        BtnCloseDetail.Clicked += (_, _) => HideDetail();
        BtnDetailOpenMap.Clicked += (_, _) => _ = OpenMapsAsync(_nearestPoi);

        // Geofence ENTER / DWELL -> highlight + TTS
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
            await PlayTtsAsync(poi);
        };
    }

    // ════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════

    private async Task<bool> EnsureLocationPermissionsAsync()
    {
        // Foreground trước
        var when = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (when != PermissionStatus.Granted) return false;

        if (DeviceInfo.Platform == DevicePlatform.Android && OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            // Android 11+: không xin kèm background; hướng user vào Settings nếu thiếu
            var always = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            if (always != PermissionStatus.Granted)
            {
                var go = await DisplayAlertAsync("Quyền nền",
                    "Để nhận geofence khi app ở nền, bật 'Allow all the time' trong Cài đặt.",
                    "Mở cài đặt", "Để sau");
                if (go) AppInfo.ShowSettingsUI();
                return false; // đợi người dùng bật rồi vào app gọi lại flow
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
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // 1. Hiển thị TPHCM ngay lập tức
        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3)));
        // 2. Xin quyền GPS
        if (!await EnsureLocationPermissionsAsync()) return;
        await MainThread.InvokeOnMainThreadAsync(() => MyMap.IsShowingUser = true);
        // 3. Load 7 POI từ SQLite lên bản đồ
        await ReloadPoisAsync();
        // 4. Lấy vị trí thực (chạy nền)
        _ = Task.Run(MoveToRealLocationAsync);
        // 5. Đăng ký Geofence + bắt GPS loop
        await _geofence.RegisterAsync(_pois);
        StartTracking();
    }

    protected override void OnDisappearing()
    {
        StopTracking();
        base.OnDisappearing();
    }

    // ════════════════════════════════════════════════════════
    // [1] HIỂN THỊ TẤT CẢ POI TỪ SQLITE
    // ════════════════════════════════════════════════════════
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

                    // Nhấn pin -> highlight + xem chi tiết
                    pin.MarkerClicked += (_, e) =>
                    {
                        e.HideInfoWindow = false;
                        HighlightPoi(p, "Đã chọn");
                        ShowDetail(p);
                    };

                    _pinMap[p.Id] = pin;
                    MyMap.Pins.Add(pin);
                }
                System.Diagnostics.Debug.WriteLine(
                    $"[Map] Hiển thị {_pois.Count} POI");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Map] Reload lỗi: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════
    // [2] HIỂN THỊ VỊ TRÍ NGƯỜI DÙNG
    // IsShowingUser="True" trong XAML đã bật dot xanh.
    // Hàm này di chuyển camera về vị trí thực.
    // ════════════════════════════════════════════════════════
    private async Task MoveToRealLocationAsync()
    {
        try
        {
            var req = new GeolocationRequest(GeolocationAccuracy.Best,
                                             TimeSpan.FromSeconds(10));
            var loc = await Geolocation.GetLocationAsync(req);
            if (loc == null) return;

            _userLocation = loc;
            await MainThread.InvokeOnMainThreadAsync(() =>
                MyMap.MoveToRegion(
                    MapSpan.FromCenterAndRadius(loc, Distance.FromKilometers(1))));

            System.Diagnostics.Debug.WriteLine(
                $"[GPS] Vị trí thực: {loc.Latitude:F6}, {loc.Longitude:F6}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GPS] {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════
    // GPS LOOP – CAMERA FOLLOW + NEAR DETECTION
    // ════════════════════════════════════════════════════════
    private void StartTracking()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = TrackLoopAsync(_cts.Token);
        _location.StartTracking((lat, lng) =>
        {
            _userLocation = new Location(lat, lng);
            System.Diagnostics.Debug.WriteLine($"[Fused] {lat:F5},{lng:F5}");
        });
    }

    void StopTracking()
    {
        _cts?.Cancel();
        _cts = null;
        _location.StopTracking();
        _ttsCts?.Cancel();
        _ttsCts = null;
    }

    private async Task TrackLoopAsync(CancellationToken token)
    {
        var req = new GeolocationRequest(GeolocationAccuracy.Medium,
                                         TimeSpan.FromSeconds(10));
        while (!token.IsCancellationRequested)
        {
            try
            {
                var loc = await Geolocation.GetLocationAsync(req, token);
                if (loc == null) { await Task.Delay(5000, token); continue; }

                _userLocation = loc;

                // Camera follow user
                //await MainThread.InvokeOnMainThreadAsync(() =>
                //    MyMap.MoveToRegion(
                //        MapSpan.FromCenterAndRadius(loc, Distance.FromMeters(350))));

                // Tìm POI gần nhất trong vùng NEAR
                Poi? nearest = null;
                double nearestDist = double.MaxValue;

                foreach (var poi in _pois.ToList())
                {
                    var dist = Location.CalculateDistance(
                        new Location(poi.Latitude, poi.Longitude),
                        loc, DistanceUnits.Kilometers) * 1000.0;

                    if (dist <= poi.NearRadiusMeters && dist < nearestDist)
                    {
                        nearest = poi;
                        nearestDist = dist;
                    }
                }

                if (nearest != null)
                {
                    // [3] HIGHLIGHT POI gần nhất
                    await MainThread.InvokeOnMainThreadAsync(() =>
                        HighlightPoi(nearest, $"Đến gần (~{nearestDist:F0}m)"));

                    // Trigger TTS nếu chưa phát gần đây
                    if (GeofenceEventGate.ShouldAccept(nearest.Id, "NEAR",
                            nearest.DebounceSeconds, nearest.CooldownSeconds))
                        await PlayTtsAsync(nearest);
                }
                else
                {
                    // Ra khỏi tất cả vùng -> bỏ highlight
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
    // [3] HIGHLIGHT POI GẦN NHẤT
    // MAUI Maps không đổi màu pin động được.
    // Cách xử lý: đổi Label pin thành "★ Tên" để phân biệt.
    // ════════════════════════════════════════════════════════
    private void HighlightPoi(Poi poi, string status)
    {
        ClearHighlight();
        _nearestPoi = poi;
        if (_pinMap.TryGetValue(poi.Id, out var pin) && pin is not null)
        pin.Label = $"★ {poi.Name}";
        ShowBanner(poi, status);
    }

    private void ClearHighlight()
    {
        if (_nearestPoi == null) return;
        if (_pinMap.TryGetValue(_nearestPoi.Id, out var pin) && pin is not null)
        pin.Label = _nearestPoi.Name;

        _nearestPoi = null;
    }
    // ════════════════════════════════════════════════════════
    // BANNER (hiện phía trên bản đồ khi có POI gần)
    // ════════════════════════════════════════════════════════
    private void ShowBanner(Poi poi, string status)
    {
        LblPoiName.Text = poi.Name;
        LblPoiDist.Text = status;
        BtnOpenMap.IsVisible = !string.IsNullOrWhiteSpace(poi.MapLink);
        PoiBanner.IsVisible = true;
    }

    private void HideBanner() => PoiBanner.IsVisible = false;

    // ════════════════════════════════════════════════════════
    // [4] XEM CHI TIẾT POI
    // Panel trượt lên từ dưới, hiển thị đầy đủ thông tin.
    // ════════════════════════════════════════════════════════
    private void ShowDetail(Poi? poi)
    {
        if (poi == null) return;

        DetailName.Text = poi.Name;
        DetailDesc.Text = string.IsNullOrWhiteSpace(poi.Description)
                                ? "(Không có mô tả)"
                                : poi.Description;
        DetailCoord.Text = $"📍 {poi.Latitude:F6}, {poi.Longitude:F6}";
        DetailRadius.Text = $"🔵 Bán kính: {poi.RadiusMeters}m  |  Vùng gần: {poi.NearRadiusMeters}m";

        BtnDetailOpenMap.IsVisible = !string.IsNullOrWhiteSpace(poi.MapLink);
        DetailPanel.IsVisible = true;

        // Zoom bản đồ về POI
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

    // ════════════════════════════════════════════════════════
    // TTS – ĐỌC THUYẾT MINH
    // ════════════════════════════════════════════════════════
    private async Task PlayTtsAsync(Poi poi, string? preferredLang = "vi-VN")
    {
        try
        {
            // Ưu tiên AudioUrl (nếu về sau bạn tích hợp audio file)
            if (!string.IsNullOrWhiteSpace(poi.AudioUrl))
            {
                System.Diagnostics.Debug.WriteLine($"[Audio] {poi.AudioUrl}");
                // TODO: gọi AudioPlayer/ NarrationManager khi bạn sẵn sàng
                return;
            }

            // Nội dung đọc (POI có NarrationText -> dùng; không có -> mô tả mặc định)
            var text = !string.IsNullOrWhiteSpace(poi.NarrationText)
                ? poi.NarrationText!
                : $"Bạn đang đến {poi.Name}. {poi.Description}";

            // Chọn locale (theo lang của POI hoặc theo máy)
            var locale = await SelectLocaleAsync(preferredLang);

            // Chặn chồng tiếng
            await _ttsGate.WaitAsync();
            try
            {
                // Huỷ cái cũ nếu còn
                _ttsCts?.Cancel();
                _ttsCts = new CancellationTokenSource();

                var opts = new SpeechOptions
                {
                    Volume = 1.0f,
                    Pitch = 1.0f,
                    Locale = locale
                };

                await TextToSpeech.Default.SpeakAsync(text, opts, _ttsCts.Token);
            }
            finally
            {
                _ttsGate.Release();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] {ex.Message}");
        }
    }
    private async Task<Microsoft.Maui.Media.Locale?> SelectLocaleAsync(string? preferred)
    {
        try
        {
            var all = await TextToSpeech.Default.GetLocalesAsync();
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                // Khớp chính xác language tag (vi-VN, en-US, ...)
                var m = all.FirstOrDefault(l =>
                    string.Equals(l.Language, preferred, StringComparison.OrdinalIgnoreCase));
                if (m != null) return m;

                // Khớp theo prefix (vi, en, ja)
                var pref = preferred.Split('-')[0];
                m = all.FirstOrDefault(l => l.Language?.StartsWith(pref, StringComparison.OrdinalIgnoreCase) == true);
                if (m != null) return m;
            }

            // Fallback theo ngôn ngữ máy (ví dụ vi-VN)
            var deviceLang = System.Globalization.CultureInfo.CurrentUICulture.Name; // e.g., vi-VN
            var byDevice = all.FirstOrDefault(l =>
                string.Equals(l.Language, deviceLang, StringComparison.OrdinalIgnoreCase));
            return byDevice ?? all.FirstOrDefault();
        }
        catch { return null; }
    }
}