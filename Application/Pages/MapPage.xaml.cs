using MauiApp1.Models;
using MauiApp1.Services.Api;
using MauiApp1.Services.Narration;
using MauiApp1.Services.Sync;

namespace MauiApp1.Pages;

public partial class MapPage : ContentPage
{
    private bool _isInitialized = false;
    private readonly IGeofenceService _geofence;
    private readonly ILocationService _location;
    private readonly PoiDatabase _db;
    private readonly NarrationManager _narration;
    private readonly PoiSyncService _poiSync;
    private readonly PlaybackApiClient _playback;
    private readonly PoiNarrationApiClient _narrationApi;
    private readonly PoiNarrationCache _narrationCache;
    private readonly TranslatorClient _translator;
    private readonly AnalyticsClient _analytics;
    private string _currentLang = LanguageService.Current;
    private readonly List<Poi> _pois = new();
    private readonly Dictionary<int, Pin> _pinMap = new();
    private CancellationTokenSource? _cts;
    private Poi? _nearestPoi;
    private Location? _userLocation;
    private static readonly Location _hcmCenter = new(10.776889, 106.700806);

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
        PlaybackApiClient playback,
        PoiNarrationApiClient narrationApi,
        PoiNarrationCache narrationCache,
        TranslatorClient translator,
        AnalyticsClient analytics)
    {
        InitializeComponent();
        _geofence = geofence ?? throw new ArgumentNullException(nameof(geofence));
        _location = location ?? throw new ArgumentNullException(nameof(location));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _narration = narration ?? throw new ArgumentNullException(nameof(narration));
        _poiSync = poiSync ?? throw new ArgumentNullException(nameof(poiSync));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _narrationApi = narrationApi ?? throw new ArgumentNullException(nameof(narrationApi));
        _narrationCache = narrationCache ?? throw new ArgumentNullException(nameof(narrationCache));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));

        // Toolbar
        ToolbarItems.Add(new ToolbarItem
        {
            Text = "QR",
            Order = ToolbarItemOrder.Primary,
            Command = new Command(async () =>
            {
                try
                {
                    var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    if (status != PermissionStatus.Granted)
                        status = await Permissions.RequestAsync<Permissions.Camera>();

                    if (status == PermissionStatus.Granted)
                        await Shell.Current!.GoToAsync("qrscan");
                    else
                        await this.DisplayAlertAsync("Từ chối", "Bạn cần cấp quyền Camera.", "OK");
                }
                catch (Exception ex)
                {
                    await this.DisplayAlertAsync("Lỗi Crash QR", ex.Message, "OK");
                }
            })
        });

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Sync",
            Order = ToolbarItemOrder.Secondary,
            Command = new Command(async () =>
            {
                try
                {
                    await _poiSync.SyncOnceAsync();
                    await ReloadPoisAsync();
                    if (_pois.Count > 0)
                        await _geofence.RegisterAsync(_pois);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Sync] Manual sync failed: {ex.Message}");
                }
            })
        });

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Reset",
            Order = ToolbarItemOrder.Secondary,
            Command = new Command(() => MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3))))
        });

        BtnOpenInMaps.Clicked += async (_, _) => await OpenMapsAsync(_nearestPoi);
        BottomSheet.SizeChanged += (_, _) => SetupBottomSheetOffsets();
        this.SizeChanged += (_, _) => SetupBottomSheetOffsets();
        _geofence.OnPoiEvent += OnGeofenceEvent;
    }

    private async void OnTopLoginClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//login");
    }

    private async void OnGeofenceEvent(Poi poi, string type)
    {
        if (type == "EXIT")
        {
            await MainThread.InvokeOnMainThreadAsync(ClearHighlight);
            return;
        }

        var evType = type == "ENTER" ? PoiEventType.Enter : PoiEventType.Near;
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            HighlightPoi(poi, $"Vào vùng {type}");
            ShowDetail(poi);
        });

        var started = DateTime.UtcNow;
        var lang = LanguageService.Current;

        var fullText = await GetNarrationTextAsync(poi.Id, evType, lang) ?? poi.NarrationText ?? poi.Description;

        // ĐÃ SỬA CẤU TRÚC ANNOUNCEMENT TẠI ĐÂY
        await _narration.HandleAsync(new Announcement(poi, lang, evType, started), overrideText: fullText);

        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
        _ = _playback.LogAsync(poi.Id, type, dur > 0 ? dur : null);
        _ = _analytics.LogVisitAsync(poi.Id, type == "ENTER" ? "enter" : "near");
        if (dur > 0) _ = _analytics.LogListenDurationAsync(poi.Id, dur);
    }

    private void RefreshLangBar()
    {
        var map = new Dictionary<string, Border>
        {
            ["vi-VN"] = BtnVi,
            ["en-US"] = BtnEn,
            ["ja-JP"] = BtnJa,
            ["ko-KR"] = BtnKo,
            ["de-DE"] = BtnDe,
        };

        foreach (var (code, btn) in map)
            btn.Background = new SolidColorBrush(code == _currentLang ? Color.FromArgb("#1976D2") : Color.FromArgb("#333333"));
    }

    private async void OnLangTapped(object? sender, TappedEventArgs e)
    {
        var code = e.Parameter as string;
        if (string.IsNullOrWhiteSpace(code)) return;
        _currentLang = code;
        LanguageService.Set(code);
        RefreshLangBar();
        try { _narration.Stop(); } catch { }
        await Task.CompletedTask;
    }

    private static byte ToEventByte(PoiEventType t) => t switch { PoiEventType.Enter => 0, PoiEventType.Near => 1, PoiEventType.Tap => 2, _ => 0 };
    private static string ToEventName(PoiEventType t) => t switch { PoiEventType.Enter => "Enter", PoiEventType.Near => "Near", PoiEventType.Tap => "Tap", _ => "Enter" };

    private async Task<string?> GetNarrationTextAsync(int poiId, PoiEventType evType, string lang, CancellationToken ct = default)
    {
        try
        {
            var evByte = ToEventByte(evType);
            var cached = await _narrationCache.GetAsync(poiId, evByte, lang);
            if (!string.IsNullOrWhiteSpace(cached)) return cached;

            if (Connectivity.Current.NetworkAccess == NetworkAccess.Internet)
            {
                var dto = await _narrationApi.GetNarrationAsync(poiId, lang, ToEventName(evType), ct);
                var text = dto?.NarrationText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    await _narrationCache.UpsertAsync(poiId, dto!.EventType, dto.Language, text);
                    return text;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NarrationFetch] {ex.Message}");
        }
        return null;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _currentLang = LanguageService.Current;
        RefreshLangBar();

        var currentUser = Preferences.Get("Username", "");
        bool isLoggedIn = !string.IsNullOrEmpty(currentUser);

        BtnTopLogin.IsVisible = !isLoggedIn;
        TopTitle.Text = isLoggedIn ? $"Xin chào, {currentUser}" : "Khách vãng lai";

        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(_hcmCenter, Distance.FromKilometers(3)));
        if (!_isInitialized)
        {
            _isInitialized = true;
            _ = InitializeMapAsync();
        }
    }

    private async Task InitializeMapAsync()
    {
        try
        {
            await _db.InitAsync();
            try { await _poiSync.SyncOnceAsync(); } catch { }

            await ReloadPoisAsync();
            if (!await EnsureLocationPermissionsAsync()) return;

            await MainThread.InvokeOnMainThreadAsync(() => MyMap.IsShowingUser = true);
            if (_pois.Count > 0)
            {
                try { await _geofence.RegisterAsync(_pois); } catch { }
            }

            _poiSync.StartAutoSync(TimeSpan.FromMinutes(2));
            _ = Task.Run(MoveToRealLocationAsync);
            StartTracking();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapInit] Fatal error: {ex}");
        }
    }

    protected override void OnDisappearing()
    {
        _geofence.OnPoiEvent -= OnGeofenceEvent;
        StopTracking();
        _poiSync.StopAutoSync();
        base.OnDisappearing();
    }

    private async Task<bool> EnsureLocationPermissionsAsync()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted) return true;

        bool userAgreed = await this.DisplayAlertAsync(
            "📍 Yêu cầu định vị",
            "Smart Tourism cần quyền truy cập vị trí để tự động phát audio.",
            "Tiếp tục", "Để sau");

        if (!userAgreed) return false;

        try { status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>(); }
        catch { }

        return status == PermissionStatus.Granted;
    }

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
                MyMap.MapElements.Clear();

                foreach (var p in pois)
                {
                    _pois.Add(p);
                    var circle = new Microsoft.Maui.Controls.Maps.Circle
                    {
                        Center = new Location(p.Latitude, p.Longitude),
                        Radius = Distance.FromMeters(p.RadiusMeters),
                        StrokeWidth = 0,
                        FillColor = Color.FromRgba(255, 69, 0, 40)
                    };
                    MyMap.MapElements.Add(circle);

                    var pin = new Pin
                    {
                        Label = p.Name,
                        Address = p.Description ?? "",
                        Location = new Location(p.Latitude, p.Longitude),
                        Type = PinType.Place
                    };
                    pin.MarkerClicked += async (_, e) =>
                    {
                        e.HideInfoWindow = false;
                        HighlightPoi(p, "Đã chọn");
                        ShowDetail(p);

                        var started = DateTime.UtcNow;
                        var lang = LanguageService.Current;
                        var fullText = await GetNarrationTextAsync(p.Id, PoiEventType.Tap, lang) ?? p.NarrationText ?? p.Description;

                        // ĐÃ SỬA CẤU TRÚC ANNOUNCEMENT TẠI ĐÂY
                        await _narration.HandleAsync(new Announcement(p, lang, PoiEventType.Tap, started), overrideText: fullText);

                        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
                        _ = _playback.LogAsync(p.Id, "TAP", dur > 0 ? dur : null);
                        _ = _analytics.LogVisitAsync(p.Id, "tap");
                        if (dur > 0) _ = _analytics.LogListenDurationAsync(p.Id, dur);
                    };
                    _pinMap[p.Id] = pin;
                    MyMap.Pins.Add(pin);
                }
            });
        }
        catch { }
    }

    private async Task MoveToRealLocationAsync()
    {
        try
        {
            var loc = await Geolocation.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10)));
            if (loc == null) return;
            _userLocation = loc;
            await MainThread.InvokeOnMainThreadAsync(() => MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(loc, Distance.FromKilometers(1))));
        }
        catch { }
    }

    private void StartTracking()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = TrackLoopAsync(_cts.Token);
        _location.StartTracking((lat, lng) => _userLocation = new Location(lat, lng));
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
        int routeTickCount = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                var loc = await Geolocation.GetLocationAsync(req, token);
                if (loc == null) { await Task.Delay(5000, token); continue; }

                if (++routeTickCount >= 6)
                {
                    routeTickCount = 0;
                    _ = _analytics.LogRouteAsync(loc.Latitude, loc.Longitude);
                }

                _userLocation = loc;
                Poi? nearest = null;
                double nearestDist = double.MaxValue;

                foreach (var poi in _pois.ToList())
                {
                    var dist = Location.CalculateDistance(new Location(poi.Latitude, poi.Longitude), loc, DistanceUnits.Kilometers) * 1000.0;
                    if (dist > poi.RadiusMeters * 2) continue;
                    if (nearest == null || dist < nearestDist)
                    {
                        nearest = poi;
                        nearestDist = dist;
                    }
                }
                if (nearest != null)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        HighlightPoi(nearest, $"Đến gần (~{nearestDist:F0}m)");
                        ShowDetail(nearest);
                    });

                    if (GeofenceEventGate.ShouldAccept(nearest.Id, "NEAR", 3, nearest.CooldownSeconds))
                    {
                        var started = DateTime.UtcNow;
                        var lang = LanguageService.Current;
                        var fullText = await GetNarrationTextAsync(nearest.Id, PoiEventType.Near, lang, token);

                        // ĐÃ SỬA CẤU TRÚC ANNOUNCEMENT TẠI ĐÂY
                        await _narration.HandleAsync(new Announcement(nearest, lang, PoiEventType.Near, started), overrideText: fullText, ct: token);

                        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
                        _ = _playback.LogAsync(nearest.Id, "NEAR", dur > 0 ? dur : null);
                    }
                }
            }
            catch { }
            try { await Task.Delay(5000, token); } catch { }
        }
    }

    private void HighlightPoi(Poi poi, string status)
    {
        ClearHighlight();
        _nearestPoi = poi;
        if (_pinMap.TryGetValue(poi.Id, out var pin)) pin.Label = $"★ {poi.Name}";
    }

    private void ClearHighlight()
    {
        if (_nearestPoi == null) return;
        if (_pinMap.TryGetValue(_nearestPoi.Id, out var pin)) pin.Label = _nearestPoi.Name;
        _nearestPoi = null;
    }

    private async void ShowDetail(Poi? poi)
    {
        if (poi == null) return;

        DetailName.Text = "Đang dịch...";
        DetailDesc.Text = "";
        DetailCoord.Text = $"📍 {poi.Latitude:F6}, {poi.Longitude:F6}";
        DetailRadius.Text = $"🔵 Bán kính: {poi.RadiusMeters}m";

        if (!string.IsNullOrWhiteSpace(poi.ImageUrl))
        {
            string imageUrl = poi.ImageUrl;
            if (!imageUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                string baseUrl = "http://192.168.1.121:5150/";
                imageUrl = baseUrl + imageUrl.TrimStart('/');
            }
            PoiImage.Source = ImageSource.FromUri(new Uri(imageUrl));
        }
        else
        {
            PoiImage.Source = null;
        }

        var lang = LanguageService.Current;
        var translatedName = lang == "vi-VN" ? poi.Name : await TranslateTextAsync(poi.Name, "vi-VN", lang);
        var translatedDesc = lang == "vi-VN" ? poi.Description : await TranslateTextAsync(poi.Description, "vi-VN", lang);

        DetailName.Text = translatedName ?? poi.Name;
        DetailDesc.Text = string.IsNullOrWhiteSpace(translatedDesc) ? "(Không có mô tả)" : translatedDesc;

        var link = !string.IsNullOrWhiteSpace(poi.MapLink) ? poi.MapLink : $"https://www.google.com/maps/search/?api=1&query={poi.Latitude},{poi.Longitude}";
        LblPoiLink.Text = link;
    }

    private async Task OpenMapsAsync(Poi? poi)
    {
        if (poi == null) return;
        var url = !string.IsNullOrWhiteSpace(poi.MapLink) ? poi.MapLink : $"https://maps.google.com/?q={poi.Latitude},{poi.Longitude}";
        try { await Launcher.OpenAsync(new Uri(url)); } catch { }
    }

    void SetupBottomSheetOffsets()
    {
        if (BottomSheet.Height <= 0 || this.Height <= 0) return;
        _sheetCollapsedOffset = Math.Max(0, BottomSheet.Height - SheetPeekHeight);
        if (!_sheetReady) { BottomSheet.TranslationY = _sheetCollapsedOffset; _sheetReady = true; }
    }

    void BottomSheet_TapHeader(object? sender, EventArgs e)
    {
        if (!_sheetReady) return;
        var isCollapsed = Math.Abs(BottomSheet.TranslationY - _sheetCollapsedOffset) < 1;
        _ = BottomSheet.TranslateToAsync(0, isCollapsed ? _sheetExpandedOffset : _sheetCollapsedOffset, 200, Easing.CubicOut);
    }

    void BottomSheet_PanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (!_sheetReady) return;
        switch (e.StatusType)
        {
            case GestureStatus.Started: _sheetStartPanY = BottomSheet.TranslationY; break;
            case GestureStatus.Running:
                BottomSheet.TranslationY = Math.Clamp(_sheetStartPanY + e.TotalY, _sheetExpandedOffset, _sheetCollapsedOffset); break;
            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var half = (_sheetCollapsedOffset - _sheetExpandedOffset) / 2.0;
                var curr = BottomSheet.TranslationY - _sheetExpandedOffset;
                _ = BottomSheet.TranslateToAsync(0, curr <= half ? _sheetExpandedOffset : _sheetCollapsedOffset, 180, Easing.CubicOut);
                break;
        }
    }

    private void OnLocateMeClicked(object? sender, EventArgs e)
    {
        if (_userLocation != null) MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(_userLocation, Distance.FromKilometers(1)));
        else _ = MoveToRealLocationAsync();
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        if (MyMap.VisibleRegion == null) return;
        var newRadius = MyMap.VisibleRegion.Radius.Kilometers * 0.5;
        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(MyMap.VisibleRegion.Center, Distance.FromKilometers(newRadius)));
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        if (MyMap.VisibleRegion == null) return;
        var newRadius = MyMap.VisibleRegion.Radius.Kilometers * 2.0;
        if (newRadius > 20000) newRadius = 20000;
        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(MyMap.VisibleRegion.Center, Distance.FromKilometers(newRadius)));
    }

    private async Task<string?> TranslateTextAsync(string? text, string fromLang, string toLang, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text) || fromLang == toLang) return text;
        try { return await _translator.TryTranslateAsync(text, toLang, fromLang, ct) ?? text; }
        catch { return text; }
    }
}