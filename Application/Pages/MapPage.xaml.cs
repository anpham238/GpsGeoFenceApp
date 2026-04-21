using MauiApp1.Models;
using MauiApp1.Services.Api;
using MauiApp1.Services.Guest;
using MauiApp1.Services.Narration;
using MauiApp1.Services.Sync;
using Microsoft.Maui.Controls.Maps;
using Syncfusion.Maui.Toolkit.BottomSheet;

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
    private readonly PoiApiClient _poiApi;
    private readonly UsageApiClient _usage;
    private readonly GuestHeartbeatService _deviceRealtime;
    private CancellationTokenSource? _searchCts;
    private List<PoiSearchResult> _searchResults = new();
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
    private readonly ProfileApiClient _profileApi; // 👈 1. THÊM DÒNG NÀY
    private Polyline? _routePolyline;
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
        AnalyticsClient analytics,
        PoiApiClient poiApi,
        ProfileApiClient profileApi,
        UsageApiClient usage,
        GuestHeartbeatService deviceRealtime)
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
        _geofence = geofence ?? throw new ArgumentNullException(nameof(geofence));
        _poiApi = poiApi ?? throw new ArgumentNullException(nameof(poiApi));
        _profileApi = profileApi;
        _usage = usage ?? throw new ArgumentNullException(nameof(usage));
        _deviceRealtime = deviceRealtime ?? throw new ArgumentNullException(nameof(deviceRealtime));
        BtnOpenInMaps.Clicked += async (_, _) => await OpenMapsAsync(_nearestPoi);
        BtnDirections.Clicked += async (_, _) => await DrawDirectionsAsync(_nearestPoi);
        BtnListen.Clicked += async (_, _) => await OnListenButtonClickedAsync();
        BottomSheet.SizeChanged += (_, _) => SetupBottomSheetOffsets();
        this.SizeChanged += (_, _) => SetupBottomSheetOffsets();
        _geofence.OnPoiEvent += OnGeofenceEvent;
    }

    private async void OnTopLoginClicked(object sender, EventArgs e)
    {
        if (AuthApiClient.IsLoggedIn())
        {
            // Đã đăng nhập thì mở trang Profile
            await Shell.Current.GoToAsync("//profile");
        }
        else
        {
            // Chưa đăng nhập thì mở trang Login
            await Shell.Current.GoToAsync("//login");
        }
    }
    private async void OnQrButtonClicked(object? sender, TappedEventArgs e)
    {
        try
        {
            var camStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (camStatus != PermissionStatus.Granted)
                camStatus = await Permissions.RequestAsync<Permissions.Camera>();

            if (camStatus == PermissionStatus.Granted)
                await Shell.Current!.GoToAsync("qrscan");
            else
                await this.DisplayAlertAsync("Từ chối", "Bạn cần cấp quyền Camera để sử dụng tính năng này.", "OK");
        }
        catch (Exception ex)
        {
            await this.DisplayAlertAsync("Lỗi QR", ex.Message, "OK");
        }
    }

    /// <summary>
    /// Cập nhật avatar: hiển thị chữ viết tắt tên user hoặc "?" nếu chưa đăng nhập.
    /// Gọi sau khi đăng nhập/đăng xuất thành công.
    /// </summary>
    private void UpdateAvatarLabel(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            AvatarLabel.Text = "?";
            AvatarBorder.BackgroundColor = Color.FromArgb("#9E9E9E");
        }
        else
        {
            // Lấy chữ cái đầu tiên của tên
            AvatarLabel.Text = displayName.Trim()[0].ToString().ToUpper();
            AvatarBorder.BackgroundColor = Color.FromArgb("#5C6BC0");
        }
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
            if (type == "ENTER")
            {
                MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(poi.Latitude, poi.Longitude), Distance.FromMeters(300)));
                if (_sheetReady) _ = BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 250, Easing.CubicOut);
            }
        });

        if (!await EnsureUsageAllowedAsync("POI_LISTEN"))
            return;

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

            if (Connectivity.Current.NetworkAccess != NetworkAccess.None)
            {
                var dto = await _narrationApi.GetNarrationAsync(poiId, lang, ToEventName(evType), ct);
                var text = dto?.NarrationText;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Dùng evByte (tính từ client) thay vì dto.EventType để key cache nhất quán
                    await _narrationCache.UpsertAsync(poiId, evByte, dto!.Language, text);
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

        var currentUser = Preferences.Get("auth_username", Preferences.Get("Username", ""));
        bool isLoggedIn = AuthApiClient.IsLoggedIn();

        if (isLoggedIn)
        {
            // Lấy link Avatar từ bộ nhớ
            var avatarUrl = Preferences.Get("auth_avatar_url", "");
            if (!string.IsNullOrWhiteSpace(avatarUrl))
            {
                // Có ảnh -> Ẩn chữ, hiện ảnh
                AvatarLabel.IsVisible = false;
                ImgAvatar.IsVisible = true;

                // ✅ Lấy BaseUrl từ _poiApi (Dùng chung cho toàn app)
                var baseUrl = _poiApi.BaseUrl.TrimEnd('/');
                ImgAvatar.Source = avatarUrl.StartsWith("http")
                    ? ImageSource.FromUri(new Uri(avatarUrl))
                    : ImageSource.FromUri(new Uri(baseUrl + "/" + avatarUrl.TrimStart('/')));
            }
            else
            {
                // Không có ảnh -> Ẩn ảnh, hiện chữ cái đầu
                ImgAvatar.IsVisible = false;
                AvatarLabel.IsVisible = true;
                UpdateAvatarLabel(currentUser);
            }
        }
        else
        {
            // Chưa đăng nhập -> Hiện dấu hỏi chấm
            ImgAvatar.IsVisible = false;
            AvatarLabel.IsVisible = true;
            UpdateAvatarLabel(null);
        }

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
                        MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(new Location(p.Latitude, p.Longitude), Distance.FromMeters(300)));
                        if (_sheetReady) _ = BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 250, Easing.CubicOut);
                        if (!await EnsureUsageAllowedAsync("POI_LISTEN"))
                            return;
                        var started = DateTime.UtcNow;
                        var lang = LanguageService.Current;
                        var fullText = await GetNarrationTextAsync(p.Id, PoiEventType.Tap, lang) ?? p.NarrationText ?? p.Description;
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
                _ = _deviceRealtime.ReportLocationAsync(loc.Latitude, loc.Longitude);
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
                        if (!await EnsureUsageAllowedAsync("POI_LISTEN"))
                            continue;

                        var started = DateTime.UtcNow;
                        var lang = LanguageService.Current;
                        var fullText = await GetNarrationTextAsync(nearest.Id, PoiEventType.Near, lang, token);
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

        // Reset image area
        PoiImage.Source = null;
        PoiImage.IsVisible = true;
        PoiImageCarousel.ItemsSource = null;
        PoiImageCarousel.IsVisible = false;
        PoiImageIndicator.IsVisible = false;

        // Load images async (không block UI)
        _ = LoadPoiImagesAsync(poi);

        var lang = LanguageService.Current;
        var translatedName = lang == "vi-VN" ? poi.Name : await TranslateTextAsync(poi.Name, "vi-VN", lang);
        var translatedDesc = lang == "vi-VN" ? poi.Description : await TranslateTextAsync(poi.Description, "vi-VN", lang);

        DetailName.Text = translatedName ?? poi.Name;
        DetailDesc.Text = string.IsNullOrWhiteSpace(translatedDesc) ? "(Không có mô tả)" : translatedDesc;

        BtnDirections.IsVisible = AuthApiClient.IsPro();
    }

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var keyword = e.NewTextValue?.Trim() ?? "";
        _searchCts?.Cancel();

        if (string.IsNullOrWhiteSpace(keyword))
        {
            SearchDropdown.IsVisible = false;
            SearchSuggestions.ItemsSource = null;
            return;
        }

        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        try
        {
            await Task.Delay(300, token);
            if (token.IsCancellationRequested) return;

            var results = await _poiApi.SearchAsync(keyword, token);
            if (token.IsCancellationRequested) return;

            _searchResults = results;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SearchSuggestions.ItemsSource = results;
                SearchDropdown.IsVisible = results.Count > 0;
            });
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Search] {ex.Message}");
        }
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        // Trigger immediate search on keyboard confirm
        var keyword = SearchEntry.Text?.Trim() ?? "";
        if (!string.IsNullOrWhiteSpace(keyword))
            _ = SearchImmediateAsync(keyword);
    }

    private async Task SearchImmediateAsync(string keyword)
    {
        try
        {
            var results = await _poiApi.SearchAsync(keyword);
            _searchResults = results;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SearchSuggestions.ItemsSource = results;
                SearchDropdown.IsVisible = results.Count > 0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchImmediate] {ex.Message}");
        }
    }

    private async void OnSuggestionSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is not PoiSearchResult selected)
            return;

        // Clear selection so user can tap same item again
        SearchSuggestions.SelectedItem = null;
        SearchDropdown.IsVisible = false;
        SearchEntry.Text = selected.Name;

        // Find matching local POI or use search result coordinates to navigate
        var localPoi = _pois.FirstOrDefault(p => p.Id == selected.Id);

        await MainThread.InvokeOnMainThreadAsync(() =>
            MyMap.MoveToRegion(MapSpan.FromCenterAndRadius(
                new Location(selected.Latitude, selected.Longitude),
                Distance.FromMeters(300))));
        if (localPoi != null)
        {
            HighlightPoi(localPoi, "Tìm kiếm");
            ShowDetail(localPoi);
            if (_sheetReady) _ = BottomSheet.TranslateToAsync(0, _sheetExpandedOffset, 250, Easing.CubicOut);
        }
    }

    private async Task OnListenButtonClickedAsync()
    {
        if (_nearestPoi == null) return;

        if (!await EnsureUsageAllowedAsync("POI_LISTEN"))
            return;

        var started = DateTime.UtcNow;
        var lang = LanguageService.Current;
        var fullText = await GetNarrationTextAsync(_nearestPoi.Id, PoiEventType.Tap, lang) ?? _nearestPoi.NarrationText ?? _nearestPoi.Description;
        await _narration.HandleAsync(new Announcement(_nearestPoi, lang, PoiEventType.Tap, started), overrideText: fullText);

        var dur = (int)(DateTime.UtcNow - started).TotalSeconds;
        _ = _playback.LogAsync(_nearestPoi.Id, "TAP", dur > 0 ? dur : null);
        _ = _analytics.LogVisitAsync(_nearestPoi.Id, "tap");
        if (dur > 0) _ = _analytics.LogListenDurationAsync(_nearestPoi.Id, dur);
    }

    private async Task<bool> EnsureUsageAllowedAsync(string actionType)
    {
        try
        {
            if (AuthApiClient.IsPro())
                return true;

            var entityId = UsageApiClient.GetEntityId();
            var (allowed, resetInHours) = await _usage.CheckAsync(entityId, actionType);
            if (allowed) return true;

            var goUpgrade = await DisplayAlertAsync(
                "Hết lượt miễn phí",
                $"Bạn đã dùng hết lượt trải nghiệm. Lượt sẽ được làm mới sau khoảng {resetInHours:F1} giờ.\n\nNâng cấp PRO để dùng không giới hạn.",
                "Nâng cấp PRO",
                "Đóng");

            if (goUpgrade)
                await Shell.Current.GoToAsync("proupgrade");

            return false;
        }
        catch
        {
            return true; // lỗi mạng → không chặn user
        }
    }

    private async Task DrawDirectionsAsync(Poi? poi)
    {
        if (poi == null) return;

        if (!AuthApiClient.IsLoggedIn())
        {
            await DisplayAlertAsync("Cần đăng nhập", "Tính năng chỉ đường chỉ dành cho tài khoản PRO. Vui lòng đăng nhập/nâng cấp.", "OK");
            return;
        }

        if (!AuthApiClient.IsPro())
        {
            var goUpgrade = await DisplayAlertAsync(
                "Chỉ đường (PRO)",
                "Tính năng chỉ đường chỉ dành cho Gói PRO. Bạn có muốn nâng cấp ngay không?",
                "Nâng cấp PRO",
                "Đóng");
            if (goUpgrade) await Shell.Current.GoToAsync("proupgrade");
            return;
        }

        var userLat = _userLocation?.Latitude;
        var userLng = _userLocation?.Longitude;
        if (!userLat.HasValue || !userLng.HasValue)
        {
            await DisplayAlertAsync("Chưa có vị trí", "Không lấy được vị trí hiện tại để chỉ đường. Vui lòng bật GPS và thử lại.", "OK");
            return;
        }

        var dto = await _profileApi.GetDirectionsAsync(poi.Id, userLat, userLng);
        if (dto?.RouteCoordinates == null || dto.RouteCoordinates.Count < 2)
        {
            await DisplayAlertAsync("Không lấy được tuyến đường", dto?.Message ?? "Vui lòng thử lại sau.", "OK");
            return;
        }

        if (_routePolyline != null)
            MyMap.MapElements.Remove(_routePolyline);

        _routePolyline = new Polyline
        {
            StrokeColor = Color.FromArgb("#1976D2"),
            StrokeWidth = 6
        };

        foreach (var c in dto.RouteCoordinates)
            _routePolyline.Geopath.Add(new Location(c.Lat, c.Lng));

        MyMap.MapElements.Add(_routePolyline);
    }

    private async Task LoadPoiImagesAsync(Poi poi)
    {
        try
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.None)
            {
                var imageUrls = await _poiApi.GetImagesAsync(poi.Id);
                if (imageUrls.Count > 0)
                {
                    var fullUrls = imageUrls.Select(ToAbsoluteUrl).ToList();
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        PoiImageCarousel.ItemsSource = fullUrls;
                        PoiImageCarousel.IsVisible = true;
                        PoiImageIndicator.IsVisible = fullUrls.Count > 1;
                        PoiImage.IsVisible = false;
                    });
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PoiImages] {ex.Message}");
        }

        // Fallback: ảnh đơn từ PoiMedia
        if (!string.IsNullOrWhiteSpace(poi.ImageUrl))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                PoiImage.Source = ImageSource.FromUri(new Uri(ToAbsoluteUrl(poi.ImageUrl!))));
        }
    }

    private string ToAbsoluteUrl(string url)
    {
        if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
        var baseUrl = _poiApi.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/{url.TrimStart('/')}";
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